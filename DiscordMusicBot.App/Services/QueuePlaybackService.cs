using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordMusicBot.App.Extensions;
using DiscordMusicBot.App.Services.Models;
using DiscordMusicBot.Core;
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

    public async Task EnqueueItemsAsync(ulong guildId, IEnumerable<PlayQueueItem> items,
        IMessageChannel? feedbackChannel = null)
    {
        var state = GetState(guildId);

        if (feedbackChannel is not null)
        {
            state.FeedbackChannel = feedbackChannel;
        }

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

    public Task<Result> ShuffleQueueAsync(ulong guildId)
    {
        var state = GetState(guildId);
        var shouldPrefetch = false;

        var shuffleResult = state.WithItems(items =>
        {
            if (items.Count <= 1)
            {
                return Result.Failure("Not enough items in the queue to shuffle.");
            }

            var startIndex = items.Count > 0 && ReferenceEquals(items[0], state.CurrentItem) ? 1 : 0;
            if (items.Count - startIndex <= 1)
            {
                return Result.Failure("Not enough items in the queue to shuffle.");
            }

            for (var i = items.Count - 1; i > startIndex; i--)
            {
                var j = Random.Shared.Next(startIndex, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }

            shouldPrefetch = true;
            return Result.Success();
        });

        if (!shuffleResult.IsSuccess)
        {
            return Task.FromResult(shuffleResult);
        }

        if (shouldPrefetch)
        {
            ClearPrefetchedTrack(state);

            _ = PrefetchTrackAsync(guildId, CancellationToken.None);
        }

        return Task.FromResult(Result.Success());
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
        ClearPrefetchedTrack(state);
    }

    public Task StartAsync(ulong guildId, IMessageChannel? feedbackChannel = null)
    {
        var state = GetState(guildId);

        if (feedbackChannel is not null)
        {
            state.FeedbackChannel = feedbackChannel;
        }

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

        ClearPrefetchedTrack(state);
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
        ClearPrefetchedTrack(state);
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
                    ClearPrefetchedTrack(state);
                    await ClearActivityAsync();

                    break;
                }

                logger.LogInformation("Now playing: '{Title}' by {Author} ({Duration}) in guild {GuildId}",
                    item.Title, item.Author ?? "Unknown", item.Duration, guildId);

                await SetActivityAsync(item.Title);

                if (item.Duration is { } d && d <= TimeSpan.Zero)
                {
                    logger.LogInformation("Track '{Title}' has zero/negative duration, skipping in guild {GuildId}",
                        item.Title, guildId);
                    await SendFeedbackAsync(guildId,
                        $"Skipping '{item.Title}'",
                        "Track has an invalid duration.");
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
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error during playback of '{Title}' in guild {GuildId}. " +
                                          "Skipping to next track.", item.Title, guildId);
                    await SendFeedbackAsync(guildId,
                        $"Error playing '{item.Title}'",
                        "An error occurred during playback. Skipping to next track.");
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
            ClearPrefetchedTrack(state);
            await ClearActivityAsync();
            await SendFeedbackAsync(guildId,
                "Playback stopped unexpectedly",
                "An unexpected error interrupted playback. Use `/queue resume` to restart.");
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
            var resolved = prefetched.ResolvedStream;
            state.PrefetchedTrack = null;

            logger.LogInformation("Launching FFmpeg for prefetched '{Title}' in guild {GuildId}",
                item.Title, guildId);

            var launchResult = await LaunchAudioStreamAsync(item, resolved, TimeSpan.Zero, streamCts.Token);
            if (!launchResult.IsSuccess)
            {
                await SendFeedbackAsync(guildId,
                    $"Failed to play '{item.Title}'",
                    $"{launchResult.ErrorMessage}. Skipping to next track.");
                return;
            }

            pcmAudioStream = launchResult.Value!;
        }
        else
        {
            ClearPrefetchedTrack(state);

            var streamResult = await AcquireAudioStreamAsync(item, startFrom, streamCts.Token);
            if (!streamResult.IsSuccess)
            {
                await SendFeedbackAsync(guildId,
                    $"Failed to play '{item.Title}'",
                    $"{streamResult.ErrorMessage}. Skipping to next track.");
                return;
            }

            pcmAudioStream = streamResult.Value!;
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

    private async Task<Result<PcmAudioStream>> AcquireAudioStreamAsync(
        PlayQueueItem item, TimeSpan startFrom, CancellationToken cancellationToken)
    {
        var resolveResult = await ResolveAudioStreamAsync(item, cancellationToken);
        if (!resolveResult.IsSuccess)
        {
            return Result<PcmAudioStream>.Failure(resolveResult.ErrorMessage!);
        }

        return await LaunchAudioStreamAsync(item, resolveResult.Value!, startFrom, cancellationToken);
    }

    private async Task<Result<ResolvedStream>> ResolveAudioStreamAsync(
        PlayQueueItem item, CancellationToken cancellationToken)
    {
        var provider = audioStreamProviderFactory.GetProvider(item.Url);

        var resolveResult = await provider.ResolveStreamAsync(item.Url, cancellationToken: cancellationToken);

        if (!resolveResult.IsSuccess)
        {
            logger.LogWarning(
                "Guild {GuildId}. Failed to resolve stream for '{Title}': {Error}.",
                item.GuildId, item.Title, resolveResult.ErrorMessage);
        }

        return resolveResult;
    }

    private async Task<Result<PcmAudioStream>> LaunchAudioStreamAsync(
        PlayQueueItem item, ResolvedStream resolved, TimeSpan startFrom, CancellationToken cancellationToken)
    {
        var provider = audioStreamProviderFactory.GetProvider(item.Url);

        var streamResult = await provider.GetAudioStreamAsync(resolved, startFrom: startFrom,
            cancellationToken: cancellationToken);

        if (!streamResult.IsSuccess)
        {
            logger.LogWarning(
                "Guild {GuildId}. Failed to launch audio stream for '{Title}': {Error}. Skipping.",
                item.GuildId, item.Title, streamResult.ErrorMessage);
        }

        return streamResult;
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

        ClearPrefetchedTrack(state);

        try
        {
            logger.LogInformation("Resolving stream for '{Title}' in guild {GuildId}", nextItem.Title, guildId);

            var resolveResult = await ResolveAudioStreamAsync(nextItem, cancellationToken);
            if (!resolveResult.IsSuccess)
            {
                return;
            }

            state.PrefetchedTrack = new PlaybackTrack
            {
                ItemId = nextItem.Id,
                ResolvedStream = resolveResult.Value!
            };

            logger.LogInformation("Prefetched stream URL for '{Title}' in guild {GuildId}", nextItem.Title, guildId);
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

    private static void ClearPrefetchedTrack(GuildPlaybackState state)
    {
        state.PrefetchedTrack = null;
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

    private async Task SendFeedbackAsync(ulong guildId, string title, string description)
    {
        var channel = GetState(guildId).FeedbackChannel;
        if (channel is null)
        {
            return;
        }

        try
        {
            var embed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(title)
                .WithDescription(description)
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send playback feedback to channel in guild {GuildId}", guildId);
        }
    }
}