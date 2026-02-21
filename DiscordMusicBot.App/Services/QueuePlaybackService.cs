using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordMusicBot.App.Extensions;
using DiscordMusicBot.App.Services.Models;
using DiscordMusicBot.Core.MusicSource.AudioStreaming;
using DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;
using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services;

public sealed partial class QueuePlaybackService(
    IAudioStreamProviderFactory audioStreamProviderFactory,
    VoiceConnectionService voiceConnectionService,
    DiscordSocketClient discordClient,
    ILogger<QueuePlaybackService> logger)
{
    private const int PcmBufferSize = 81920; // ~0.85s of 48kHz 16-bit stereo PCM.
    private const int PcmBytesPerSecond = 192000; // 48kHz * 16-bit * 2 channels.

    private readonly ConcurrentDictionary<ulong, GuildPlaybackState> _states = new();

    public bool IsPlaying(ulong guildId)
    {
        return GetState(guildId).IsPlaying;
    }

    public PlayQueueItem? GetCurrentItem(ulong guildId)
    {
        return GetState(guildId).CurrentItem;
    }

    public IReadOnlyCollection<PlayQueueItem> GetQueueItems(ulong guildId, int skip = 0, int take = 10)
    {
        var state = GetState(guildId);
        return state.WithItems(items => items
            .Skip(skip)
            .Take(take)
            .ToArray());
    }

    public async Task EnqueueItemsAsync(ulong guildId, IEnumerable<PlayQueueItem> items)
    {
        var state = GetState(guildId);
        state.WithItems(list => list.AddRange(items));

        if (state is { IsPlaying: false, IsConnected: true })
        {
            await StartAsync(guildId);
        }
        else if (!state.IsPlaying)
        {
            _ = PrefetchTrackAsync(guildId, CancellationToken.None);
        }
    }

    public async Task ShuffleQueueAsync(ulong guildId)
    {
        var state = GetState(guildId);
        var shouldPrefetch = false;

        state.WithItems(items =>
        {
            if (items.Count <= 1)
            {
                return;
            }

            var startIndex = items.Count > 0 && ReferenceEquals(items[0], state.CurrentItem) ? 1 : 0;
            if (items.Count - startIndex <= 1)
            {
                return;
            }

            for (var i = items.Count - 1; i > startIndex; i--)
            {
                var j = Random.Shared.Next(startIndex, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }

            shouldPrefetch = true;
        });

        if (!shouldPrefetch)
        {
            return;
        }

        await DisposePrefetchedTrackAsync(state);

        _ = PrefetchTrackAsync(guildId, CancellationToken.None);
    }

    public async Task ClearQueueAsync(ulong guildId)
    {
        var state = GetState(guildId);

        if (state.IsPlaying)
        {
            await FullStopAsync(guildId);
        }

        state.ResumePosition = TimeSpan.Zero;
        state.ResumeItemId = null;
        state.WithItems(items => items.Clear());
        await DisposePrefetchedTrackAsync(state);
    }

    public Task StartAsync(ulong guildId)
    {
        var state = GetState(guildId);

        if (state.IsPlaying)
        {
            logger.LogInformation("Queue is already playing in guild {GuildId}", guildId);
            return Task.CompletedTask;
        }

        var hasItems = state.WithItems(items => items.Count > 0);
        if (!hasItems)
        {
            logger.LogInformation("Queue is empty in guild {GuildId}, nothing to start", guildId);
            return Task.CompletedTask;
        }

        state.PauseCts = new CancellationTokenSource();
        state.IsPlaying = true;

        logger.LogInformation("Starting queue playback in guild {GuildId}", guildId);

        _ = RunAdvancementLoopAsync(guildId);
        return Task.CompletedTask;
    }

    public async Task PauseAsync(ulong guildId)
    {
        if (!_states.TryGetValue(guildId, out var state) || !state.IsPlaying)
        {
            logger.LogInformation("Queue is not playing in guild {GuildId}, nothing to stop", guildId);
            return;
        }

        logger.LogInformation("Pausing queue playback in guild {GuildId}", guildId);

        var currentItem = state.CurrentItem;
        CancelPlayback(state);

        if (currentItem is not null)
        {
            state.WithItems(items => items.Insert(0, currentItem));
        }

        await DisposePrefetchedTrackAsync(state);
        await ClearActivityAsync();
    }

    private async Task FullStopAsync(ulong guildId)
    {
        var state = GetState(guildId);
        if (!state.IsPlaying)
        {
            logger.LogInformation("Queue is not playing in guild {GuildId}, nothing to stop", guildId);
            return;
        }

        logger.LogInformation("Resetting queue playback in guild {GuildId}", guildId);

        state.ResumePosition = TimeSpan.Zero;
        state.ResumeItemId = null;
        CancelPlayback(state);
        await DisposePrefetchedTrackAsync(state);
        await ClearActivityAsync();
    }

    public Task<(PlayQueueItem? Skipped, PlayQueueItem? Next)> SkipAsync(ulong guildId)
    {
        var state = GetState(guildId);
        if (!state.IsPlaying)
        {
            logger.LogInformation("Queue is not playing in guild {GuildId}. Cannot skip", guildId);
            return Task.FromResult(new ValueTuple<PlayQueueItem?, PlayQueueItem?>(null, null));
        }

        logger.LogInformation("Skipping current track in guild {GuildId}", guildId);

        var currentItem = state.CurrentItem;
        var nextItem = state.WithItems(items => items.FirstOrDefault());
        state.ResumePosition = TimeSpan.Zero;
        state.ResumeItemId = null;
        try
        {
            state.SkipCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }

        return Task.FromResult(new ValueTuple<PlayQueueItem?, PlayQueueItem?>(currentItem, nextItem));
    }

    private GuildPlaybackState GetState(ulong guildId)
    {
        return _states.GetOrAdd(guildId, new GuildPlaybackState());
    }

    private async Task RunAdvancementLoopAsync(ulong guildId)
    {
        var state = GetState(guildId);
        var pauseToken = state.PauseCts!.Token;

        try
        {
            while (state.IsPlaying && !pauseToken.IsCancellationRequested)
            {
                state.CurrentItem = state.WithItems(items => items.Pop());
                var item = state.CurrentItem;

                if (item is null)
                {
                    logger.LogInformation("Queue is empty in guild {GuildId}. Auto-stopping playback.", guildId);

                    state.ResumePosition = TimeSpan.Zero;
                    state.ResumeItemId = null;
                    CancelPlayback(state);
                    await DisposePrefetchedTrackAsync(state);
                    await ClearActivityAsync();

                    break;
                }

                logger.LogInformation("Now playing: '{Title}' by {Author} ({Duration}) in guild {GuildId}",
                    item.Title, item.Author ?? "Unknown", item.Duration, guildId);

                await SetActivityAsync(item.Title);

                if (item.Duration is null || item.Duration.Value <= TimeSpan.Zero)
                {
                    logger.LogInformation("Track '{Title}' has no duration, skipping to next in guild {GuildId}",
                        item.Title, guildId);
                    continue;
                }

                var skipCts = CancellationTokenSource.CreateLinkedTokenSource(pauseToken);
                state.SkipCts = skipCts;

                try
                {
                    await StreamToVoiceAsync(guildId);
                }
                catch (OperationCanceledException) when (skipCts.IsCancellationRequested
                                                         && !pauseToken.IsCancellationRequested)
                {
                    logger.LogInformation("Track '{Title}' was skipped in guild {GuildId}", item.Title, guildId);
                }
                finally
                {
                    skipCts.Dispose();
                    state.SkipCts = null;
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Queue advancement loop cancelled in guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in queue advancement loop for guild {GuildId}", guildId);
            state.ResumePosition = TimeSpan.Zero;
            state.ResumeItemId = null;
            CancelPlayback(state);
            state.CurrentItem = null;
            await DisposePrefetchedTrackAsync(state);
            await ClearActivityAsync();
        }
    }

    private async Task StreamToVoiceAsync(ulong guildId)
    {
        var state = GetState(guildId);
        var item = state.CurrentItem!;
        var skipToken = state.SkipCts!.Token;
        var pauseToken = state.PauseCts!.Token;

        var audioClient = voiceConnectionService.GetConnection(guildId);
        if (audioClient is null)
        {
            throw new InvalidOperationException(
                $"Not in voice channel for guild {guildId}. Cannot play tracks");
        }

        var startFrom = state.ResumeItemId == item.Id ? state.ResumePosition : TimeSpan.Zero;
        state.ResumePosition = TimeSpan.Zero;
        state.ResumeItemId = null;

        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(pauseToken, skipToken);

        PcmAudioStream pcmAudioStream;

        if (state.PrefetchedTrack is { } prefetched && prefetched.ItemId == item.Id && startFrom == TimeSpan.Zero)
        {
            pcmAudioStream = prefetched.Stream;
            state.PrefetchedTrack = null;
            logger.LogInformation("Using prefetched stream for '{Title}' in guild {GuildId}", item.Title, guildId);
        }
        else
        {
            await DisposePrefetchedTrackAsync(state);

            var acquiredStream = await AcquireAudioStreamAsync(item, startFrom, streamCts.Token);
            if (acquiredStream is null)
            {
                return;
            }

            pcmAudioStream = acquiredStream;
        }

        await using (pcmAudioStream)
        {
            logger.LogInformation("Streaming audio for '{Title}' in guild {GuildId} (from {StartFrom})",
                item.Title, guildId, startFrom);

            _ = PrefetchTrackAsync(guildId, pauseToken);

            var discordStream = state.DiscordPcmStream;
            if (discordStream is null || state.DiscordPcmStreamOwner != audioClient)
            {
                if (discordStream is not null)
                {
                    await discordStream.FlushAsync(CancellationToken.None);
                    await discordStream.DisposeAsync();
                }

                discordStream = audioClient.CreatePCMStream(AudioApplication.Music);
                state.DiscordPcmStream = discordStream;
                state.DiscordPcmStreamOwner = audioClient;
            }

            try
            {
                var buffer = new byte[PcmBufferSize];
                long bytesWritten = 0;

                try
                {
                    int bytesRead;
                    while ((bytesRead = await pcmAudioStream.Stream.ReadAsync(buffer, skipToken)) > 0)
                    {
                        await discordStream.WriteAsync(buffer.AsMemory(0, bytesRead), skipToken);
                        bytesWritten += bytesRead;
                    }
                }
                finally
                {
                    if (pauseToken.IsCancellationRequested)
                    {
                        state.ResumePosition =
                            startFrom + TimeSpan.FromSeconds((double)bytesWritten / PcmBytesPerSecond);
                        state.ResumeItemId = item.Id;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error streaming audio for '{Title}' in guild {GuildId}. " +
                                      "Resetting Discord PCM stream.", item.Title, guildId);

                state.DiscordPcmStream = null;
                state.DiscordPcmStreamOwner = null;

                try
                {
                    await discordStream.DisposeAsync();
                }
                catch
                {
                    //
                }

                throw;
            }

            logger.LogInformation("Finished streaming '{Title}' in guild {GuildId}", item.Title, guildId);
        }
    }

    private async Task<PcmAudioStream?> AcquireAudioStreamAsync(
        PlayQueueItem item, TimeSpan startFrom, CancellationToken cancellationToken)
    {
        var provider = audioStreamProviderFactory.GetProvider(item.Url);

        var streamResult = await provider.GetAudioStreamAsync(item.Url, startFrom: startFrom,
            cancellationToken: cancellationToken);

        if (streamResult.IsSuccess)
        {
            return streamResult.Value!;
        }

        logger.LogWarning(
            "Guild {GuildId}. Failed to get audio stream for '{Title}': {Error}. Skipping.",
            item.GuildId, item.Title, streamResult.ErrorMessage);

        return null;
    }

    private async Task PrefetchTrackAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var state = GetState(guildId);

        var nextItem = state.WithItems(items => items.FirstOrDefault());
        if (nextItem is null)
        {
            return;
        }

        if (state.PrefetchedTrack?.ItemId == nextItem.Id)
        {
            return;
        }

        await DisposePrefetchedTrackAsync(state);

        try
        {
            logger.LogInformation("Prefetching audio for '{Title}' in guild {GuildId}", nextItem.Title, guildId);

            var stream = await AcquireAudioStreamAsync(nextItem, TimeSpan.Zero, cancellationToken);
            if (stream is null)
            {
                return;
            }

            state.PrefetchedTrack = new PlaybackTrack { ItemId = nextItem.Id, Stream = stream };

            logger.LogInformation("Prefetched audio for '{Title}' in guild {GuildId}", nextItem.Title, guildId);
        }
        catch (OperationCanceledException)
        {
            // Expected when playback is paused or stopped during prefetch.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to prefetch audio for '{Title}' in guild {GuildId}",
                nextItem.Title, guildId);
        }
    }

    private static async Task DisposePrefetchedTrackAsync(GuildPlaybackState state)
    {
        var prefetched = state.PrefetchedTrack;
        if (prefetched is null)
        {
            return;
        }

        state.PrefetchedTrack = null;
        await prefetched.DisposeAsync();
    }

    private static void CancelPlayback(GuildPlaybackState state)
    {
        state.IsPlaying = false;

        if (state.DiscordPcmStream is not null)
        {
            var discordPcmStream = state.DiscordPcmStream;
            state.DiscordPcmStream = null;
            state.DiscordPcmStreamOwner = null;
            _ = DisposeDiscordPcmStreamAsync(discordPcmStream);
        }

        SafeCancelAndDispose(ref state.PauseCts);
        SafeCancelAndDispose(ref state.SkipCts);
    }

    private static void SafeCancelAndDispose(ref CancellationTokenSource? cts)
    {
        var snapshot = Interlocked.Exchange(ref cts, null);
        if (snapshot is null)
        {
            return;
        }

        try
        {
            snapshot.Cancel();
            snapshot.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static async Task DisposeDiscordPcmStreamAsync(AudioOutStream stream)
    {
        try
        {
            await stream.FlushAsync(CancellationToken.None);
            await stream.DisposeAsync();
        }
        catch
        {
            //
        }
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