using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordMusicBot.App.Services.Models;
using DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;
using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services;

public sealed class QueuePlaybackService(
    IServiceScopeFactory scopeFactory,
    IAudioStreamProviderFactory audioStreamProviderFactory,
    VoiceConnectionService voiceConnectionService,
    DiscordSocketClient discordClient,
    ILogger<QueuePlaybackService> logger)
{
    private const int PcmBufferSize = 81920; // ~0.85s of 48kHz 16-bit stereo PCM.

    private readonly ConcurrentDictionary<ulong, GuildPlaybackState> _states = new();

    public event Func<ulong, PlayQueueItem, Task>? TrackStarted;
    public event Func<ulong, Task>? PlaybackStopped;

    public bool IsPlaying(ulong guildId)
    {
        return _states.TryGetValue(guildId, out var state) && state.IsPlaying;
    }

    public PlayQueueItem? GetCurrentItem(ulong guildId)
    {
        return _states.TryGetValue(guildId, out var state) ? state.CurrentItem : null;
    }

    public TimeSpan? GetElapsed(ulong guildId)
    {
        if (!_states.TryGetValue(guildId, out var state))
        {
            return null;
        }

        var sinceResume = state.TrackStartedAtUtc is not null
            ? DateTime.UtcNow - state.TrackStartedAtUtc.Value
            : TimeSpan.Zero;

        return state.ElapsedBeforePause + sinceResume;
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

    public async Task StopAsync(ulong guildId)
    {
        if (!_states.TryGetValue(guildId, out var state) || !state.IsPlaying)
        {
            logger.LogInformation("Queue is not playing in guild {GuildId}, nothing to stop", guildId);
            return;
        }

        logger.LogInformation("Stopping queue playback in guild {GuildId}", guildId);
        CancelAndReset(state);
        await ClearActivityAsync();

        if (PlaybackStopped is not null)
        {
            await PlaybackStopped.Invoke(guildId);
        }
    }

    public void Skip(ulong guildId)
    {
        if (!_states.TryGetValue(guildId, out var state) || !state.IsPlaying)
        {
            logger.LogInformation("Queue is not playing in guild {GuildId}, nothing to skip", guildId);
            return;
        }

        logger.LogInformation("Skipping current track in guild {GuildId}", guildId);

        try
        {
            state.SkipCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }
    }

    public Task OnVoiceConnected(ulong guildId)
    {
        if (!_states.TryGetValue(guildId, out var state) || !state.IsPlaying)
        {
            return Task.CompletedTask;
        }

        logger.LogInformation("Voice connected in guild {GuildId} while queue is playing.", guildId);

        state.VoiceConnectedTcs?.TrySetResult();

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
                var item = state.CurrentItem;

                if (item is null)
                {
                    using (var scope = scopeFactory.CreateScope())
                    {
                        var repository = scope.ServiceProvider.GetRequiredService<IPlayQueueRepository>();
                        item = await repository.PeekAsync(guildId, cancellationToken);
                    }

                    if (item is null)
                    {
                        logger.LogInformation("Queue is empty in guild {GuildId}. Auto-stopping playback.", guildId);
                        CancelAndReset(state);
                        await ClearActivityAsync();

                        if (PlaybackStopped is not null)
                        {
                            await PlaybackStopped.Invoke(guildId);
                        }

                        break;
                    }

                    state.CurrentItem = item;
                }

                logger.LogInformation(
                    "Now playing: '{Title}' by {Author} ({Duration}) in guild {GuildId}",
                    item.Title, item.Author ?? "Unknown", item.Duration, guildId);

                if (TrackStarted is not null)
                {
                    await TrackStarted.Invoke(guildId, item);
                }

                await SetActivityAsync(item.Title);

                if (item.Duration is null || item.Duration.Value <= TimeSpan.Zero)
                {
                    logger.LogInformation("Track '{Title}' has no duration, skipping to next in guild {GuildId}",
                        item.Title, guildId);

                    await RemoveItemAsync(item.Id, cancellationToken);
                    state.CurrentItem = null;
                    state.ElapsedBeforePause = TimeSpan.Zero;
                    continue;
                }

                var skipCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                state.SkipCts = skipCts;

                try
                {
                    await PlayTrackAsync(guildId, item, state, state.ElapsedBeforePause, skipCts.Token);
                }
                catch (OperationCanceledException) when (skipCts.IsCancellationRequested
                                                         && !cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation("Track '{Title}' was skipped in guild {GuildId}", item.Title, guildId);
                }
                finally
                {
                    skipCts.Dispose();
                    state.SkipCts = null;
                    state.TrackStartedAtUtc = null;
                }

                await RemoveItemAsync(item.Id, cancellationToken);
                state.CurrentItem = null;
                state.ElapsedBeforePause = TimeSpan.Zero;
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
            state.CurrentItem = null;
            await ClearActivityAsync();

            if (PlaybackStopped is not null)
            {
                await PlaybackStopped.Invoke(guildId);
            }
        }
    }

    private async Task RemoveItemAsync(long itemId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPlayQueueRepository>();
            await repository.RemoveAsync(itemId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove item {ItemId} from queue", itemId);
        }
    }

    private async Task PlayTrackAsync(ulong guildId, PlayQueueItem item, GuildPlaybackState state,
        TimeSpan startFrom, CancellationToken cancellationToken)
    {
        while (true)
        {
            var audioClient = voiceConnectionService.GetConnection(guildId);

            if (audioClient is not null)
            {
                state.VoiceConnectedTcs = null;
                await StreamToVoiceAsync(guildId, item, state, startFrom, audioClient, cancellationToken);
                return;
            }

            logger.LogInformation(
                "Not in voice channel for guild {GuildId}. Advancing silently for '{Title}'.",
                guildId, item.Title);

            state.TrackStartedAtUtc = DateTime.UtcNow;

            var remaining = item.Duration!.Value - startFrom;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            var voiceTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            state.VoiceConnectedTcs = voiceTcs;

            using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var delayTask = Task.Delay(remaining, delayCts.Token);

            var completed = await Task.WhenAny(delayTask, voiceTcs.Task);

            if (completed == delayTask)
            {
                await delayTask; // Propagate cancellation if cancelled.
                state.VoiceConnectedTcs = null;
                return;
            }

            await delayCts.CancelAsync();

            state.VoiceConnectedTcs = null;
            startFrom = state.ElapsedBeforePause + (DateTime.UtcNow - (state.TrackStartedAtUtc ?? DateTime.UtcNow));
            state.TrackStartedAtUtc = null;

            logger.LogInformation(
                "Voice connected mid-track in guild {GuildId}. Resuming '{Title}' from {Elapsed}.",
                guildId, item.Title, startFrom);
        }
    }

    private async Task StreamToVoiceAsync(ulong guildId, PlayQueueItem item, GuildPlaybackState state,
        TimeSpan startFrom, IAudioClient audioClient, CancellationToken cancellationToken)
    {
        IAudioStreamProvider provider;
        try
        {
            provider = audioStreamProviderFactory.GetProvider(item.Url);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex,
                "No audio stream provider for '{Url}' in guild {GuildId}. Advancing silently.",
                item.Url, guildId);

            state.TrackStartedAtUtc = DateTime.UtcNow;

            var remaining = item.Duration!.Value - startFrom;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }

            return;
        }

        var streamResult = await provider.GetAudioStreamAsync(item.Url, startFrom, cancellationToken);

        if (!streamResult.IsSuccess)
        {
            logger.LogWarning(
                "Failed to get audio stream for '{Title}' in guild {GuildId}: {Error}. Advancing silently.",
                item.Title, guildId, streamResult.ErrorMessage);

            state.TrackStartedAtUtc = DateTime.UtcNow;

            var remaining = item.Duration!.Value - startFrom;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }

            return;
        }

        await using var pcmAudioStream = streamResult.Value!;

        state.TrackStartedAtUtc = DateTime.UtcNow;

        logger.LogInformation("Streaming audio for '{Title}' in guild {GuildId}", item.Title, guildId);

        await using var discordStream = audioClient.CreatePCMStream(AudioApplication.Music);

        try
        {
            var buffer = new byte[PcmBufferSize];
            int bytesRead;

            while ((bytesRead = await pcmAudioStream.Stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await discordStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }
        finally
        {
            await discordStream.FlushAsync(CancellationToken.None);
        }

        logger.LogInformation("Finished streaming '{Title}' in guild {GuildId}", item.Title, guildId);
    }

    private static void CancelAndReset(GuildPlaybackState state)
    {
        state.IsPlaying = false;

        if (state.TrackStartedAtUtc is not null)
        {
            state.ElapsedBeforePause += DateTime.UtcNow - state.TrackStartedAtUtc.Value;
            state.TrackStartedAtUtc = null;
        }

        state.VoiceConnectedTcs?.TrySetCanceled();
        state.VoiceConnectedTcs = null;

        try
        {
            state.SkipCts?.Cancel();
            state.SkipCts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            //
        }

        state.SkipCts = null;

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

    private async Task SetActivityAsync(string trackTitle)
    {
        try
        {
            await discordClient.SetActivityAsync(new Game(trackTitle, ActivityType.Listening));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set activity status");
        }
    }

    private async Task ClearActivityAsync()
    {
        try
        {
            await discordClient.SetActivityAsync(null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear activity status");
        }
    }
}
