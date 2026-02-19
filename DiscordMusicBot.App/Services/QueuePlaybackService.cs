using System.Collections.Concurrent;
using DiscordMusicBot.App.Services.Models;
using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services;

public sealed class QueuePlaybackService(
    IServiceScopeFactory scopeFactory,
    ILogger<QueuePlaybackService> logger)
{
    private readonly ConcurrentDictionary<ulong, GuildPlaybackState> _states = new();

    public bool IsPlaying(ulong guildId)
    {
        return _states.TryGetValue(guildId, out var state) && state.IsPlaying;
    }

    public PlayQueueItem? GetCurrentItem(ulong guildId)
    {
        return _states.TryGetValue(guildId, out var state) ? state.CurrentItem : null;
    }

    public Task StartAsync(ulong guildId)
    {
        var state = _states.GetOrAdd(guildId, _ => new GuildPlaybackState());

        if (state.IsPlaying)
        {
            logger.LogInformation("Queue is already playing in guild {GuildId}", guildId);
            return Task.CompletedTask;
        }

        state.Cts = new CancellationTokenSource();
        state.IsPlaying = true;

        logger.LogInformation("Starting queue playback in guild {GuildId}", guildId);

        _ = RunAdvancementLoopAsync(guildId, state);

        return Task.CompletedTask;
    }

    public Task StopAsync(ulong guildId)
    {
        if (!_states.TryGetValue(guildId, out var state) || !state.IsPlaying)
        {
            logger.LogInformation("Queue is not playing in guild {GuildId}, nothing to stop", guildId);
            return Task.CompletedTask;
        }

        logger.LogInformation("Stopping queue playback in guild {GuildId}", guildId);
        CancelAndReset(state);

        return Task.CompletedTask;
    }

    public Task OnVoiceConnected(ulong guildId)
    {
        if (IsPlaying(guildId))
        {
            logger.LogInformation(
                "Voice connected in guild {GuildId} while queue is playing. Audio output will start when implemented.",
                guildId);
        }

        return Task.CompletedTask;
    }

    public Task OnVoiceDisconnected(ulong guildId)
    {
        if (IsPlaying(guildId))
        {
            logger.LogInformation(
                "Voice disconnected in guild {GuildId}. Queue continues advancing silently.", guildId);
        }

        return Task.CompletedTask;
    }

    private async Task RunAdvancementLoopAsync(ulong guildId, GuildPlaybackState state)
    {
        var cancellationToken = state.Cts?.Token ?? CancellationToken.None;

        try
        {
            while (state.IsPlaying && !cancellationToken.IsCancellationRequested)
            {
                PlayQueueItem? item;

                using (var scope = scopeFactory.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IPlayQueueRepository>();
                    item = await repository.DequeueAsync(guildId, cancellationToken);
                }

                if (item is null)
                {
                    logger.LogInformation("Queue is empty in guild {GuildId}. Auto-stopping playback.", guildId);
                    CancelAndReset(state);
                    break;
                }

                state.CurrentItem = item;

                logger.LogInformation(
                    "Now playing: '{Title}' by {Author} ({Duration}) in guild {GuildId}",
                    item.Title, item.Author ?? "Unknown", item.Duration, guildId);

                if (item.Duration is null || item.Duration.Value <= TimeSpan.Zero)
                {
                    logger.LogInformation("Track '{Title}' has no duration, skipping to next in guild {GuildId}",
                        item.Title, guildId);
                    continue;
                }

                await Task.Delay(item.Duration.Value, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Queue advancement loop cancelled in guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in queue advancement loop for guild {GuildId}", guildId);
            CancelAndReset(state);
        }
    }

    private static void CancelAndReset(GuildPlaybackState state)
    {
        state.IsPlaying = false;
        state.CurrentItem = null;

        try
        {
            state.Cts?.Cancel();
            state.Cts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            //
        }

        state.Cts = null;
    }
}