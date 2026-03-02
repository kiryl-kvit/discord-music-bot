using DiscordMusicBot.App.Services.Common;
using DiscordMusicBot.App.Services.Discord;
using DiscordMusicBot.App.Services.Voice;
using System.Buffers;
using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using DiscordMusicBot.Core;
using DiscordMusicBot.Core.MusicSource;
using DiscordMusicBot.Core.MusicSource.AudioStreaming;
using DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;
using DiscordMusicBot.Core.MusicSource.Related;
using DiscordMusicBot.Domain.History;
using DiscordMusicBot.Domain.Playback;
using DiscordMusicBot.Domain.PlayQueue;
using DiscordMusicBot.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services.Queue;

public sealed partial class QueuePlaybackService(
    IAudioStreamProviderFactory audioStreamProviderFactory,
    VoiceConnectionService voiceConnectionService,
    IPlayQueueRepository queueRepository,
    IGuildPlaybackStateRepository stateRepository,
    IHistoryRepository historyRepository,
    IGuildSettingsRepository guildSettingsRepository,
    IRelatedTrackProvider relatedTrackProvider,
    DiscordApiService discordApiService,
    ILogger<QueuePlaybackService> logger)
{
    private const int PcmBufferSize = 81920; // ~0.85s of 48kHz 16-bit stereo PCM.
    private const int PcmBytesPerSecond = 192000; // 48kHz * 16-bit * 2 channels.
    private const int PreBufferSize = PcmBytesPerSecond * 5; // ~5s of audio to pre-buffer before playback.
    private const int AutoplayExcludeCount = 50;

    private readonly ConcurrentDictionary<ulong, GuildPlaybackState> _states = new();

    public bool IsPlaying(ulong guildId)
    {
        return GetState(guildId).IsPlaying;
    }

    public PlayQueueItem? GetCurrentItem(ulong guildId)
    {
        return GetState(guildId).CurrentItem;
    }

    public TimeSpan GetElapsedTime(ulong guildId)
    {
        return GetState(guildId).GetElapsedTime();
    }

    public IMessageChannel? GetFeedbackChannel(ulong guildId)
    {
        return GetState(guildId).FeedbackChannel;
    }

    public async Task<IReadOnlyCollection<PlayQueueItem>> GetQueueItemsAsync(ulong guildId, int skip = 0,
        int take = 10, CancellationToken cancellationToken = default)
    {
        var state = GetState(guildId);
        var offset = 0;

        if (state.CurrentItem is { } currentItem)
        {
            var headItem = await queueRepository.PeekNextAsync(guildId, cancellationToken: cancellationToken);
            if (headItem?.Id == currentItem.Id)
            {
                offset = 1;
            }
        }

        return await queueRepository.GetPageAsync(guildId, skip + offset, take, cancellationToken);
    }

    public async Task<QueueStats> GetQueueStatsAsync(ulong guildId,
        CancellationToken cancellationToken = default)
    {
        var state = GetState(guildId);
        var excludeId = state.CurrentItem?.Id;

        var (count, totalDurationMs) =
            await queueRepository.GetCountAndTotalDurationMsAsync(guildId, excludeId, cancellationToken);

        return new QueueStats(count, TimeSpan.FromMilliseconds(totalDurationMs));
    }

    public async Task EnqueueItemsAsync(ulong guildId, IEnumerable<PlayQueueItem> items,
        IMessageChannel? feedbackChannel = null, CancellationToken cancellationToken = default)
    {
        var state = GetState(guildId);

        if (feedbackChannel is not null)
        {
            state.FeedbackChannel = feedbackChannel;
        }

        var itemsList = items as IReadOnlyList<PlayQueueItem> ?? items.ToArray();
        await queueRepository.AddItemsAsync(guildId, itemsList, cancellationToken);

        if (state is { IsPlaying: false, IsConnected: true })
        {
            await StartAsync(guildId, cancellationToken: cancellationToken);
        }
        else if (!state.IsPlaying)
        {
            _ = PrefetchTrackAsync(guildId, CancellationToken.None);
        }
    }

    public async Task<Result> ShuffleQueueAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        var state = GetState(guildId);
        var currentItemId = state.CurrentItem?.Id;

        var count = await queueRepository.GetCountAsync(guildId, cancellationToken);
        var shuffleableCount = currentItemId.HasValue ? count - 1 : count;
        if (shuffleableCount <= 1)
        {
            return Result.Failure("Not enough items in the queue to shuffle.");
        }

        await queueRepository.ShuffleAsync(guildId, excludeItemId: currentItemId,
            cancellationToken: cancellationToken);

        state.ClearPrefetchedTrack();
        _ = PrefetchTrackAsync(guildId, CancellationToken.None);

        return Result.Success();
    }

    public async Task ClearQueueAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        var state = GetState(guildId);

        if (state.IsPlaying)
        {
            var loopTask = state.PlaybackLoopTask;
            await FullStopAsync(guildId, cancellationToken);
            if (loopTask is not null)
            {
                await loopTask;
            }
        }

        state.ResetResumeState();
        state.ClearPrefetchedTrack();
        await queueRepository.ClearAsync(guildId, cancellationToken);
        await ClearPersistedStateAsync(guildId, cancellationToken);
    }

    public async Task<Result> StartAsync(ulong guildId, IMessageChannel? feedbackChannel = null,
        CancellationToken cancellationToken = default)
    {
        var state = GetState(guildId);

        if (feedbackChannel is not null)
        {
            state.FeedbackChannel = feedbackChannel;
        }

        if (state.IsPlaying)
        {
            logger.LogInformation("Queue is already playing in guild {GuildId}", guildId);
            return Result.Success();
        }

        var count = await queueRepository.GetCountAsync(guildId, cancellationToken);
        if (count == 0)
        {
            logger.LogInformation("Queue is empty in guild {GuildId}, nothing to start", guildId);
            return Result.Failure("Queue is empty.");
        }

        state.PauseCts = new CancellationTokenSource();
        state.IsPlaying = true;

        logger.LogInformation("Starting queue playback in guild {GuildId}", guildId);

        var isResuming = state.ResumeItemId is not null;

        state.PlaybackLoopTask = RunAdvancementLoopAsync(guildId);

        if (isResuming)
        {
            await RaisePlaybackResumedAsync(guildId);
        }

        return Result.Success();
    }

    public async Task PauseAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        if (!_states.TryGetValue(guildId, out var state) || !state.IsPlaying)
        {
            logger.LogInformation("Queue is not playing in guild {GuildId}, nothing to stop", guildId);
            return;
        }

        logger.LogInformation("Pausing queue playback in guild {GuildId}", guildId);

        state.PlaybackStartOffset = state.GetElapsedTime();
        state.PlaybackStartedUtc = null;

        state.CancelPlayback();

        if (state.PlaybackLoopTask is { } loopTask)
        {
            state.PlaybackLoopTask = null;
            await loopTask;
        }

        await PersistStateAsync(guildId, state, cancellationToken);

        await discordApiService.ClearNowPlayingAsync(guildId, GetState(guildId).VoiceChannelId);
        await RaisePlaybackPausedAsync(guildId);
    }

    private async Task FullStopAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var state = GetState(guildId);
        if (!state.IsPlaying)
        {
            logger.LogInformation("Queue is not playing in guild {GuildId}, nothing to stop", guildId);
            return;
        }

        logger.LogInformation("Resetting queue playback in guild {GuildId}", guildId);

        state.FullReset();
        await ClearPersistedStateAsync(guildId, cancellationToken);
        await discordApiService.ClearNowPlayingAsync(guildId, state.VoiceChannelId);
    }

    public async Task<SkipResult> SkipAsync(ulong guildId,
        int count = 1, CancellationToken cancellationToken = default)
    {
        var state = GetState(guildId);
        if (!state.IsPlaying)
        {
            logger.LogInformation("Queue is not playing in guild {GuildId}. Cannot skip", guildId);
            return new SkipResult(null, 0, null);
        }

        logger.LogInformation("Skipping {Count} track(s) in guild {GuildId}", count, guildId);

        var currentItem = state.CurrentItem;

        var removedFromQueue = 0;
        if (count > 1)
        {
            removedFromQueue = await queueRepository.DeleteTopNAsync(guildId, count - 1,
                excludeItemId: currentItem?.Id, cancellationToken: cancellationToken);
            state.ClearPrefetchedTrack();
        }

        var nextItem = await queueRepository.PeekNextAsync(guildId, skip: 1, cancellationToken: cancellationToken);
        state.ResetResumeState();
        state.TriggerSkip();

        var totalSkipped = removedFromQueue + 1;
        return new SkipResult(currentItem, totalSkipped, nextItem);
    }

    public async Task GracefulStopAsync(CancellationToken cancellationToken = default)
    {
        var playingGuildIds = _states
            .Where(kvp => kvp.Value.IsPlaying)
            .Select(kvp => kvp.Key)
            .ToArray();

        if (playingGuildIds.Length == 0)
        {
            return;
        }

        logger.LogInformation("Gracefully stopping playback in {Count} guild(s)", playingGuildIds.Length);

        foreach (var guildId in playingGuildIds)
        {
            try
            {
                await PauseAsync(guildId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to gracefully stop playback in guild {GuildId}", guildId);
            }
        }
    }

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PersistedGuildState> persisted;

        try
        {
            persisted = await stateRepository.GetAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load persisted guild playback states");
            return;
        }

        if (persisted.Count == 0)
        {
            logger.LogInformation("No persisted guild playback states to restore");
            return;
        }

        logger.LogInformation("Restoring playback state for {Count} guild(s)", persisted.Count);

        foreach (var saved in persisted)
        {
            await RestoreGuildAsync(saved, cancellationToken);
        }
    }

    private async Task RestoreGuildAsync(PersistedGuildState saved, CancellationToken cancellationToken)
    {
        var guildId = saved.GuildId;

        try
        {
            var voiceChannel = discordApiService.GetVoiceChannel(guildId, saved.VoiceChannelId);
            if (voiceChannel is null)
            {
                logger.LogWarning(
                    "Voice channel {ChannelId} not found in guild {GuildId} during restore; clearing persisted state",
                    saved.VoiceChannelId, guildId);
                await ClearPersistedStateAsync(guildId, cancellationToken);
                return;
            }

            var queueCount = await queueRepository.GetCountAsync(guildId, cancellationToken);
            if (queueCount == 0)
            {
                logger.LogInformation("Queue is empty for guild {GuildId} during restore; clearing persisted state",
                    guildId);
                await ClearPersistedStateAsync(guildId, cancellationToken);
                return;
            }

            var state = GetState(guildId);
            state.ResumePosition = saved.ResumePosition;
            state.ResumeItemId = saved.ResumeItemId;

            if (saved.FeedbackChannelId is { } feedbackId)
            {
                state.FeedbackChannel = discordApiService.GetTextChannel(guildId, feedbackId);
            }

            logger.LogInformation(
                "Restoring guild {GuildId}: joining voice channel {ChannelId}, resume position {ResumePosition}",
                guildId, saved.VoiceChannelId, saved.ResumePosition);

            await voiceConnectionService.JoinAsync(voiceChannel, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to restore playback state for guild {GuildId}", guildId);
            await ClearPersistedStateAsync(guildId, cancellationToken);
        }
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
                await RaiseTrackLoadingAsync(guildId);

                state.CurrentItem = await queueRepository.PeekNextAsync(guildId,
                    cancellationToken: pauseToken);
                var item = state.CurrentItem;

                if (item is null)
                {
                    var fillResult = await TryFillAutoplayQueueAsync(guildId, currentItem: null, pauseToken);
                    if (fillResult == AutoplayFillResult.Filled)
                    {
                        continue;
                    }

                    if (fillResult == AutoplayFillResult.NoCompatibleSeed)
                    {
                        await DisableAutoplayWithFeedbackAsync(guildId, pauseToken);
                    }

                    logger.LogInformation("Queue is empty in guild {GuildId}. Auto-stopping playback.", guildId);
                    await FullStopAsync(guildId, pauseToken);
                    await RaisePlaybackStoppedAsync(guildId);
                    break;
                }

                logger.LogInformation("Now playing: '{Title}' by {Author} ({Duration}) in guild {GuildId}",
                    item.Title, item.Author ?? "Unknown", item.Duration, guildId);

                if (state.ResumeItemId != item.Id)
                {
                    state.ResetElapsedTime();
                }

                await discordApiService.SetNowPlayingAsync(guildId, state.VoiceChannelId, item.Title);
                await RaiseTrackStartedAsync(guildId, item);

                if (item.Duration is { } d && d <= TimeSpan.Zero)
                {
                    logger.LogInformation("Track '{Title}' has zero/negative duration, skipping in guild {GuildId}",
                        item.Title, guildId);
                    await SendFeedbackAsync(guildId,
                        $"Skipping '{item.Title}'",
                        "Track has an invalid duration.",
                        pauseToken);
                    await queueRepository.MarkAsPlayedAsync(item.Id, pauseToken);
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
                        "An error occurred during playback. Skipping to next track.",
                        pauseToken);
                }
                finally
                {
                    skipCts.Dispose();
                    state.SkipCts = null;
                }

                await queueRepository.MarkAsPlayedAsync(item.Id, pauseToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Queue advancement loop cancelled in guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in queue advancement loop for guild {GuildId}", guildId);
            state.FullReset();
            await ClearPersistedStateAsync(guildId, CancellationToken.None);
            await discordApiService.ClearNowPlayingAsync(guildId, state.VoiceChannelId);
            await SendFeedbackAsync(guildId,
                "Playback stopped unexpectedly",
                "An unexpected error interrupted playback. Use `/queue resume` to restart.",
                CancellationToken.None);
            await RaisePlaybackStoppedAsync(guildId);
        }
    }

    private async Task TryProactiveAutoplayFillAsync(ulong guildId, PlayQueueItem? currentItem,
        CancellationToken cancellationToken)
    {
        try
        {
            var nextItem = await queueRepository.PeekNextAsync(guildId, skip: 1,
                cancellationToken: cancellationToken);
            if (nextItem is not null)
            {
                return;
            }

            await TryFillAutoplayQueueAsync(guildId, currentItem, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when playback is paused or stopped.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Proactive autoplay fill failed in guild {GuildId}", guildId);
        }
    }

    private async Task<AutoplayFillResult> TryFillAutoplayQueueAsync(ulong guildId, PlayQueueItem? currentItem,
        CancellationToken cancellationToken)
    {
        var state = GetState(guildId);

        if (!state.TryBeginAutoplayFill())
        {
            return AutoplayFillResult.Skipped;
        }

        try
        {
            var settings = await guildSettingsRepository.GetAsync(guildId, cancellationToken);
            if (settings is not { AutoplayEnabled: true })
            {
                return AutoplayFillResult.Skipped;
            }

            SourceType[] autoplaySourceTypes = [SourceType.YouTube, SourceType.Spotify];
            string? seedUrl = null;

            // Prefer the currently playing track as seed (it may not be in history yet).
            if (currentItem is not null && autoplaySourceTypes.Contains(currentItem.SourceType))
            {
                seedUrl = currentItem.Url;
            }

            // Fall back to history.
            if (seedUrl is null)
            {
                var lastPlayed = await historyRepository.GetLastPlayedAsync(guildId, cancellationToken);
                if (lastPlayed is not null && autoplaySourceTypes.Contains(lastPlayed.SourceType))
                {
                    seedUrl = lastPlayed.Url;
                }
            }

            if (seedUrl is null)
            {
                var compatibleItem =
                    await historyRepository.GetLastPlayedBySourceTypesAsync(guildId, autoplaySourceTypes,
                        cancellationToken);
                if (compatibleItem is not null)
                {
                    seedUrl = compatibleItem.Url;
                }
            }

            if (seedUrl is null)
            {
                logger.LogInformation(
                    "No compatible YouTube/Spotify seed found for autoplay in guild {GuildId}", guildId);
                return AutoplayFillResult.NoCompatibleSeed;
            }

            var excludeUrls =
                await historyRepository.GetRecentUrlsAsync(guildId, AutoplayExcludeCount, cancellationToken);

            var excludeSet = new HashSet<string>(excludeUrls, StringComparer.OrdinalIgnoreCase);

            // Also exclude the currently playing track so it doesn't appear in recommendations.
            if (currentItem is not null)
            {
                excludeSet.Add(currentItem.Url);
            }

            var queueSize = settings.AutoplayQueueSize;
            var relatedTracks = await relatedTrackProvider.GetRelatedTracksAsync(
                seedUrl, excludeSet.ToList(), queueSize, cancellationToken);

            if (relatedTracks.Count == 0)
            {
                logger.LogInformation("No related tracks found for autoplay in guild {GuildId}", guildId);
                return AutoplayFillResult.Failed;
            }

            var botUserId = discordApiService.BotUserId;
            var queueItems = relatedTracks.Select(track =>
                PlayQueueItem.Create(guildId, botUserId, track.SourceType, track.Url, track.Title,
                    track.Author, track.Duration, track.ThumbnailUrl)).ToList();

            await queueRepository.AddItemsAsync(guildId, queueItems, cancellationToken);

            logger.LogInformation("Autoplay enqueued {Count} tracks in guild {GuildId}",
                queueItems.Count, guildId);

            return AutoplayFillResult.Filled;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Autoplay fill failed in guild {GuildId}", guildId);
            return AutoplayFillResult.Failed;
        }
        finally
        {
            state.EndAutoplayFill();
        }
    }

    private async Task DisableAutoplayWithFeedbackAsync(ulong guildId, CancellationToken cancellationToken)
    {
        try
        {
            await guildSettingsRepository.SetAutoplayAsync(guildId, false, cancellationToken);
            logger.LogInformation("Autoplay disabled due to no compatible seed in guild {GuildId}", guildId);

            var channel = GetState(guildId).FeedbackChannel;
            if (channel is not null)
            {
                var embed = QueueEmbedBuilder.BuildAutoplayDisabledEmbed();
                await channel.SendMessageAsync(embed: embed,
                    options: new RequestOptions { CancelToken = cancellationToken });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to disable autoplay with feedback in guild {GuildId}", guildId);
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
        state.ResumeItemId = item.Id;
        state.ResumePosition = startFrom;

        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(pauseToken, skipToken);

        var streamResult = await AcquireOrUsePrefetchedStreamAsync(state, item, startFrom, streamCts.Token);
        if (!streamResult.IsSuccess)
        {
            await SendFeedbackAsync(guildId,
                $"Failed to play '{item.Title}'",
                $"{streamResult.ErrorMessage}. Skipping to next track.",
                pauseToken);
            return;
        }

        await using var pcmAudioStream = streamResult.Value!;

        logger.LogInformation("Streaming audio for '{Title}' in guild {GuildId} (from {StartFrom})",
            item.Title, guildId, startFrom);

        state.PlaybackStartOffset = startFrom;
        state.PlaybackStartedUtc = DateTimeOffset.UtcNow;

        _ = PrefetchTrackAsync(guildId, pauseToken);
        _ = TryProactiveAutoplayFillAsync(guildId, state.CurrentItem, pauseToken);

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
            var buffer = ArrayPool<byte>.Shared.Rent(PcmBufferSize);
            try
            {
                long bytesWritten = 0;

                // ── Pre-buffer phase ──────────────────────────────────────
                // Fill ~5 s of PCM data before writing anything to Discord.
                // This builds a buffer reserve that absorbs CDN / network
                // jitter during the critical first seconds of playback.
                var preBuffer = ArrayPool<byte>.Shared.Rent(PreBufferSize);
                try
                {
                    var preBuffered = 0;
                    while (preBuffered < PreBufferSize)
                    {
                        var read = await pcmAudioStream.Stream.ReadAsync(
                            preBuffer.AsMemory(preBuffered, PreBufferSize - preBuffered), skipToken);
                        if (read == 0)
                            break;
                        preBuffered += read;
                    }

                    // Write the pre-buffered data to Discord in chunks.
                    var offset = 0;
                    while (offset < preBuffered)
                    {
                        var chunk = Math.Min(PcmBufferSize, preBuffered - offset);
                        await discordStream.WriteAsync(preBuffer.AsMemory(offset, chunk), skipToken);
                        offset += chunk;
                        bytesWritten += chunk;
                        state.ResumePosition =
                            startFrom + TimeSpan.FromSeconds((double)bytesWritten / PcmBytesPerSecond);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(preBuffer);
                }

                // ── Normal streaming loop ─────────────────────────────────
                // The large Pipe buffer in FfmpegAudioPipeline keeps FFmpeg
                // writing ahead at CDN speed, so ReadAsync almost never blocks.
                int bytesRead;
                while ((bytesRead = await pcmAudioStream.Stream.ReadAsync(
                           buffer.AsMemory(0, PcmBufferSize), skipToken)) > 0)
                {
                    await discordStream.WriteAsync(buffer.AsMemory(0, bytesRead), skipToken);
                    bytesWritten += bytesRead;
                    state.ResumePosition =
                        startFrom + TimeSpan.FromSeconds((double)bytesWritten / PcmBytesPerSecond);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
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
            state.ResetDiscordStream(discordStream);
            throw;
        }

        state.ResetResumeState();
        state.ResetElapsedTime();
        logger.LogInformation("Finished streaming '{Title}' in guild {GuildId}", item.Title, guildId);
    }

    private async Task<Result<PcmAudioStream>> AcquireOrUsePrefetchedStreamAsync(
        GuildPlaybackState state, PlayQueueItem item, TimeSpan startFrom, CancellationToken cancellationToken)
    {
        ResolvedStream? resolved = null;

        if (state.PrefetchedTrack is { } prefetched && prefetched.ItemId == item.Id && startFrom == TimeSpan.Zero)
        {
            resolved = prefetched.ResolvedStream;
            state.ClearPrefetchedTrack();

            logger.LogInformation("Launching FFmpeg for prefetched '{Title}' in guild {GuildId}",
                item.Title, item.GuildId);
        }
        else if (state.CurrentTrack is { } cached && cached.ItemId == item.Id)
        {
            resolved = cached.ResolvedStream;

            logger.LogInformation("Reusing cached stream for '{Title}' in guild {GuildId}",
                item.Title, item.GuildId);
        }

        if (resolved is not null)
        {
            state.CurrentTrack = new PlaybackTrack { ItemId = item.Id, ResolvedStream = resolved };
            return await LaunchAudioStreamAsync(item, resolved, startFrom, cancellationToken);
        }

        state.ClearPrefetchedTrack();

        var resolveResult = await ResolveAudioStreamAsync(item, cancellationToken);
        if (!resolveResult.IsSuccess)
        {
            return Result<PcmAudioStream>.Failure(resolveResult.ErrorMessage!);
        }

        state.CurrentTrack = new PlaybackTrack { ItemId = item.Id, ResolvedStream = resolveResult.Value! };
        return await LaunchAudioStreamAsync(item, resolveResult.Value!, startFrom, cancellationToken);
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

        var nextItem = await queueRepository.PeekNextAsync(guildId, skip: 1,
            cancellationToken: cancellationToken);
        if (nextItem is null)
        {
            return;
        }

        if (state.PrefetchedTrack?.ItemId == nextItem.Id)
        {
            return;
        }

        state.ClearPrefetchedTrack();

        try
        {
            logger.LogInformation("Resolving stream for '{Title}' in guild {GuildId}", nextItem.Title, guildId);

            var versionBeforeResolve = state.PrefetchVersion;

            var resolveResult = await ResolveAudioStreamAsync(nextItem, cancellationToken);
            if (!resolveResult.IsSuccess)
            {
                return;
            }

            if (state.PrefetchVersion != versionBeforeResolve)
            {
                logger.LogInformation(
                    "Prefetch for '{Title}' in guild {GuildId} discarded (version changed during resolve)",
                    nextItem.Title, guildId);
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

    private async Task PersistStateAsync(ulong guildId, GuildPlaybackState state,
        CancellationToken cancellationToken)
    {
        var voiceChannelId = voiceConnectionService.GetVoiceChannelId(guildId) ?? state.VoiceChannelId;
        if (voiceChannelId is null)
        {
            return;
        }

        var feedbackChannelId = state.FeedbackChannel is IEntity<ulong> entity ? entity.Id : (ulong?)null;

        try
        {
            await stateRepository.SaveAsync(new PersistedGuildState(
                guildId,
                voiceChannelId.Value,
                feedbackChannelId,
                state.ResumePosition,
                state.ResumeItemId), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist playback state for guild {GuildId}", guildId);
        }
    }

    private async Task ClearPersistedStateAsync(ulong guildId, CancellationToken cancellationToken)
    {
        try
        {
            await stateRepository.DeleteAsync(guildId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear persisted playback state for guild {GuildId}", guildId);
        }
    }

    private async Task SendFeedbackAsync(ulong guildId, string title, string description,
        CancellationToken cancellationToken)
    {
        var channel = GetState(guildId).FeedbackChannel;
        if (channel is null)
        {
            return;
        }

        try
        {
            var embed = ErrorEmbedBuilder.Build(title, description);

            await channel.SendMessageAsync(embed: embed,
                options: new RequestOptions { CancelToken = cancellationToken });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send playback feedback to channel in guild {GuildId}", guildId);
        }
    }
}