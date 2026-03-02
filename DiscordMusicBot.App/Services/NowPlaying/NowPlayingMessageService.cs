using System.Collections.Concurrent;
using Discord;
using Discord.Net;
using DiscordMusicBot.Domain.PlayQueue;
using DiscordMusicBot.Domain.Settings;
using DiscordMusicBot.App.Services.Queue;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services.NowPlaying;

public sealed class NowPlayingMessageService(
    QueuePlaybackService queuePlaybackService,
    IPlayQueueRepository queueRepository,
    IGuildSettingsRepository guildSettingsRepository,
    ILogger<NowPlayingMessageService> logger)
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromSeconds(1);

    private readonly ConcurrentDictionary<ulong, NowPlayingMessageState> _states = new();

    public async Task OnTrackStartedAsync(ulong guildId, PlayQueueItem _)
    {
        if (!_states.TryGetValue(guildId, out var msgState))
        {
            return;
        }

        var info = await BuildNowPlayingInfoAsync(guildId);
        if (info is null)
        {
            return;
        }

        var embed = NowPlayingEmbedBuilder.BuildEmbed(info);
        var components = NowPlayingEmbedBuilder.BuildComponents(info.IsPaused);

        if (await TryModifyMessageAsync(msgState, guildId, embed, components))
        {
            Interlocked.Exchange(ref msgState.LastEditUtcTicks, DateTimeOffset.UtcNow.Ticks);
            StopTimer(msgState);
            StartTimer(guildId, msgState);
        }
        else if (_states.TryRemove(guildId, out var removed))
        {
            StopTimer(removed);
        }
    }

    public async Task OnTrackLoadingAsync(ulong guildId)
    {
        if (!_states.TryGetValue(guildId, out var msgState))
        {
            return;
        }

        await UpdateToLoadingStateAsync(guildId, msgState);
    }

    public async Task OnPlaybackPausedAsync(ulong guildId)
    {
        await UpdateMessageAsync(guildId);
    }

    public async Task OnPlaybackResumedAsync(ulong guildId)
    {
        await UpdateMessageAsync(guildId);
    }

    public async Task OnPlaybackStoppedAsync(ulong guildId)
    {
        if (!_states.TryRemove(guildId, out var msgState))
        {
            return;
        }

        StopTimer(msgState);
        await UpdateToStoppedStateAsync(guildId, msgState);
    }

    public void RegisterCommandResponse(ulong guildId, IMessageChannel channel, ulong messageId)
    {
        if (_states.TryRemove(guildId, out var oldState))
        {
            StopTimer(oldState);
            _ = TryDeleteMessageAsync(oldState, guildId);
        }

        var newState = new NowPlayingMessageState
        {
            ChannelId = channel.Id,
            MessageId = messageId,
            Channel = channel,
            LastEditUtcTicks = DateTimeOffset.UtcNow.Ticks,
        };

        _states[guildId] = newState;
        StartTimer(guildId, newState);
    }

    public async Task<NowPlayingInfo?> BuildNowPlayingInfoAsync(ulong guildId,
        CancellationToken cancellationToken = default)
    {
        var currentItem = queuePlaybackService.GetCurrentItem(guildId);
        if (currentItem is null)
        {
            return null;
        }

        var isPaused = !queuePlaybackService.IsPlaying(guildId);
        var elapsed = queuePlaybackService.GetElapsedTime(guildId);

        var nextItemTask = queueRepository.PeekNextAsync(guildId, skip: 1, cancellationToken: cancellationToken);
        var statsTask = queuePlaybackService.GetQueueStatsAsync(guildId, cancellationToken);
        var settingsTask = guildSettingsRepository.GetAsync(guildId, cancellationToken);
        await Task.WhenAll(nextItemTask, statsTask, settingsTask);

        var nextItem = nextItemTask.Result;
        var stats = statsTask.Result;
        var settings = settingsTask.Result;

        return new NowPlayingInfo
        {
            Item = currentItem,
            IsPaused = isPaused,
            Elapsed = elapsed,
            NextItem = nextItem,
            QueueCount = stats.Count,
            QueueTotalDuration = stats.TotalDuration > TimeSpan.Zero ? stats.TotalDuration : null,
            IsAutoplayEnabled = settings?.AutoplayEnabled ?? false,
        };
    }

    public void StopAll()
    {
        foreach (var kvp in _states)
        {
            if (_states.TryRemove(kvp.Key, out var state))
            {
                StopTimer(state);
            }
        }
    }

    public async Task<bool> StopAsync(ulong guildId)
    {
        if (!_states.TryRemove(guildId, out var msgState))
        {
            return false;
        }

        StopTimer(msgState);
        await TryDeleteMessageAsync(msgState, guildId);
        return true;
    }

    private async Task UpdateMessageAsync(ulong guildId)
    {
        if (!_states.TryGetValue(guildId, out var msgState))
        {
            return;
        }

        var info = await BuildNowPlayingInfoAsync(guildId);
        if (info is null)
        {
            return;
        }

        var embed = NowPlayingEmbedBuilder.BuildEmbed(info);
        var components = NowPlayingEmbedBuilder.BuildComponents(info.IsPaused);

        if (!await TryModifyMessageAsync(msgState, guildId, embed, components))
        {
            if (_states.TryRemove(guildId, out var removed))
            {
                StopTimer(removed);
            }
        }
        else
        {
            Interlocked.Exchange(ref msgState.LastEditUtcTicks, DateTimeOffset.UtcNow.Ticks);
        }
    }

    private async Task UpdateToLoadingStateAsync(ulong guildId, NowPlayingMessageState msgState)
    {
        if (DateTimeOffset.UtcNow.Ticks - Interlocked.Read(ref msgState.LastEditUtcTicks) < DebounceInterval.Ticks)
        {
            return;
        }

        var embed = NowPlayingEmbedBuilder.BuildLoadingEmbed();
        var components = NowPlayingEmbedBuilder.BuildDisabledComponents();

        if (await TryModifyMessageAsync(msgState, guildId, embed, components))
        {
            Interlocked.Exchange(ref msgState.LastEditUtcTicks, DateTimeOffset.UtcNow.Ticks);
        }
    }

    private async Task UpdateToStoppedStateAsync(ulong guildId, NowPlayingMessageState msgState)
    {
        var embed = NowPlayingEmbedBuilder.BuildStoppedEmbed();
        var components = NowPlayingEmbedBuilder.BuildDisabledComponents();

        await TryModifyMessageAsync(msgState, guildId, embed, components);
    }

    private async Task<bool> TryModifyMessageAsync(NowPlayingMessageState msgState, ulong guildId,
        Embed embed, MessageComponent components)
    {
        try
        {
            await msgState.Channel.ModifyMessageAsync(msgState.MessageId, props =>
            {
                props.Embed = embed;
                props.Components = components;
            });
            return true;
        }
        catch (HttpException ex) when (ex.HttpCode is System.Net.HttpStatusCode.NotFound
                                           or System.Net.HttpStatusCode.Forbidden)
        {
            logger.LogInformation(
                "Now-playing message no longer accessible in guild {GuildId} (HTTP {StatusCode})",
                guildId, (int)ex.HttpCode);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to edit now-playing message in guild {GuildId}", guildId);
            return false;
        }
    }

    private async Task TryDeleteMessageAsync(NowPlayingMessageState msgState, ulong guildId)
    {
        try
        {
            await msgState.Channel.DeleteMessageAsync(msgState.MessageId);
        }
        catch (HttpException ex) when (ex.HttpCode is System.Net.HttpStatusCode.NotFound
                                           or System.Net.HttpStatusCode.Forbidden)
        {
            // Already deleted or inaccessible.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete old now-playing message in guild {GuildId}", guildId);
        }
    }

    private void StartTimer(ulong guildId, NowPlayingMessageState msgState)
    {
        var cts = new CancellationTokenSource();
        msgState.TimerCts = cts;
        _ = RunPeriodicUpdateLoopAsync(guildId, cts.Token);
    }

    private static void StopTimer(NowPlayingMessageState msgState)
    {
        var cts = Interlocked.Exchange(ref msgState.TimerCts, null);
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
            cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RunPeriodicUpdateLoopAsync(ulong guildId, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(UpdateInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_states.TryGetValue(guildId, out var msgState)
                    && DateTimeOffset.UtcNow.Ticks - Interlocked.Read(ref msgState.LastEditUtcTicks) < DebounceInterval.Ticks)
                {
                    continue;
                }

                try
                {
                    await UpdateMessageAsync(guildId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Periodic now-playing update failed for guild {GuildId}", guildId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on stop.
        }
    }
}