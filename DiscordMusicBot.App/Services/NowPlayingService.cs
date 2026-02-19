using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using DiscordMusicBot.App.Services.Models;
using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services;

public sealed class NowPlayingService(
    DiscordSocketClient discordClient,
    QueuePlaybackService playbackService,
    IServiceScopeFactory scopeFactory,
    ILogger<NowPlayingService> logger)
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<ulong, NowPlayingState> _states = new();

    public async Task ActivateAsync(ulong guildId, IMessageChannel channel)
    {
        if (_states.TryRemove(guildId, out var existing))
        {
            await existing.TimerCts.CancelAsync();
            existing.TimerCts.Dispose();
            await TryDeleteMessageAsync(existing);
        }

        var currentItem = playbackService.GetCurrentItem(guildId);
        var isPlaying = playbackService.IsPlaying(guildId);

        Embed embed;
        MessageComponent components;

        if (currentItem is not null && isPlaying)
        {
            var elapsed = playbackService.GetElapsed(guildId);
            var queueRemaining = await GetQueueCountAsync(guildId);
            embed = NowPlayingEmbedBuilder.BuildNowPlayingEmbed(currentItem, elapsed, queueRemaining);
            components = NowPlayingEmbedBuilder.BuildPlayerControls(isPlaying: true);
        }
        else
        {
            embed = NowPlayingEmbedBuilder.BuildStoppedEmbed();
            components = NowPlayingEmbedBuilder.BuildPlayerControls(isPlaying: false);
        }

        var message = await channel.SendMessageAsync(embed: embed, components: components);

        var state = new NowPlayingState
        {
            ChannelId = channel.Id,
            MessageId = message.Id,
            Message = message,
            TimerCts = new CancellationTokenSource(),
        };

        _states[guildId] = state;

        if (isPlaying)
        {
            _ = RunUpdateLoopAsync(guildId, state);
        }

        logger.LogInformation("Now Playing panel activated in guild {GuildId}, channel {ChannelId}",
            guildId, channel.Id);
    }

    public async Task DismissAsync(ulong guildId)
    {
        if (!_states.TryRemove(guildId, out var state))
        {
            return;
        }

        await state.TimerCts.CancelAsync();
        state.TimerCts.Dispose();
        await TryDeleteMessageAsync(state);

        logger.LogInformation("Now Playing panel dismissed in guild {GuildId}", guildId);
    }

    public async Task OnTrackStartedAsync(ulong guildId, PlayQueueItem item)
    {
        if (!_states.TryGetValue(guildId, out var state))
        {
            return;
        }

        await state.TimerCts.CancelAsync();
        state.TimerCts.Dispose();

        await TryDeleteMessageAsync(state);

        var channel = await GetChannelAsync(guildId, state.ChannelId);
        if (channel is null)
        {
            _states.TryRemove(guildId, out _);
            return;
        }

        var elapsed = playbackService.GetElapsed(guildId);
        var queueRemaining = await GetQueueCountAsync(guildId);
        var embed = NowPlayingEmbedBuilder.BuildNowPlayingEmbed(item, elapsed, queueRemaining);
        var components = NowPlayingEmbedBuilder.BuildPlayerControls(isPlaying: true);

        var message = await channel.SendMessageAsync(embed: embed, components: components);

        state.MessageId = message.Id;
        state.Message = message;
        state.TimerCts = new CancellationTokenSource();

        _ = RunUpdateLoopAsync(guildId, state);
    }

    public async Task OnPlaybackStoppedAsync(ulong guildId)
    {
        if (!_states.TryGetValue(guildId, out var state))
        {
            return;
        }

        await state.TimerCts.CancelAsync();
        state.TimerCts.Dispose();
        state.TimerCts = new CancellationTokenSource();

        await TryModifyMessageAsync(state, msg =>
        {
            msg.Embed = NowPlayingEmbedBuilder.BuildStoppedEmbed();
            msg.Components = NowPlayingEmbedBuilder.BuildPlayerControls(isPlaying: false);
        });
    }

    public bool IsActive(ulong guildId) => _states.ContainsKey(guildId);

    public async Task RefreshAsync(ulong guildId)
    {
        if (!_states.TryGetValue(guildId, out var state))
        {
            return;
        }

        var currentItem = playbackService.GetCurrentItem(guildId);
        var isPlaying = playbackService.IsPlaying(guildId);

        if (currentItem is not null && isPlaying)
        {
            var elapsed = playbackService.GetElapsed(guildId);
            var queueRemaining = await GetQueueCountAsync(guildId);

            await TryModifyMessageAsync(state, msg =>
            {
                msg.Embed = NowPlayingEmbedBuilder.BuildNowPlayingEmbed(currentItem, elapsed, queueRemaining);
                msg.Components = NowPlayingEmbedBuilder.BuildPlayerControls(isPlaying: true);
            });
        }
        else
        {
            await TryModifyMessageAsync(state, msg =>
            {
                msg.Embed = NowPlayingEmbedBuilder.BuildStoppedEmbed();
                msg.Components = NowPlayingEmbedBuilder.BuildPlayerControls(isPlaying: false);
            });
        }
    }

    private async Task RunUpdateLoopAsync(ulong guildId, NowPlayingState state)
    {
        var token = state.TimerCts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(UpdateInterval, token);

                var currentItem = playbackService.GetCurrentItem(guildId);
                var isPlaying = playbackService.IsPlaying(guildId);

                if (currentItem is null || !isPlaying)
                {
                    break;
                }

                var elapsed = playbackService.GetElapsed(guildId);
                var queueRemaining = await GetQueueCountAsync(guildId);

                await TryModifyMessageAsync(state, msg =>
                {
                    msg.Embed = NowPlayingEmbedBuilder.BuildNowPlayingEmbed(currentItem, elapsed, queueRemaining);
                    msg.Components = NowPlayingEmbedBuilder.BuildPlayerControls(isPlaying: true);
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when track changes or playback stops.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error in now-playing update loop for guild {GuildId}", guildId);
        }
    }

    private async Task<IMessageChannel?> GetChannelAsync(ulong guildId, ulong channelId)
    {
        try
        {
            var guild = discordClient.GetGuild(guildId);
            var channel = guild?.GetTextChannel(channelId);
            if (channel is not null)
            {
                return channel;
            }

            var genericChannel = await discordClient.GetChannelAsync(channelId);
            return genericChannel as IMessageChannel;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get channel {ChannelId} in guild {GuildId}", channelId, guildId);
            return null;
        }
    }

    private async Task<int> GetQueueCountAsync(ulong guildId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPlayQueueRepository>();
            var items = await repository.GetAllAsync(guildId);
            return items.Count;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get queue count for guild {GuildId}", guildId);
            return 0;
        }
    }

    private static async Task TryDeleteMessageAsync(NowPlayingState state)
    {
        try
        {
            if (state.Message is not null)
            {
                await state.Message.DeleteAsync();
            }
        }
        catch
        {
            // Message may already be deleted.
        }
    }

    private static async Task TryModifyMessageAsync(NowPlayingState state, Action<MessageProperties> modify)
    {
        try
        {
            if (state.Message is not null)
            {
                await state.Message.ModifyAsync(modify);
            }
        }
        catch
        {
            // Message may have been deleted.
        }
    }
}