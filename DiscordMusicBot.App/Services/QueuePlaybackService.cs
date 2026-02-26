using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordMusicBot.App.Services.Models;
using DiscordMusicBot.Core;
using DiscordMusicBot.Core.MusicSource.AudioStreaming;
using DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;
using DiscordMusicBot.Domain.Playback;
using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services;

public sealed partial class QueuePlaybackService(
    IAudioStreamProviderFactory audioStreamProviderFactory,
    VoiceConnectionService voiceConnectionService,
    IPlayQueueRepository queueRepository,
    IGuildPlaybackStateRepository stateRepository,
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

    public async Task<IReadOnlyCollection<PlayQueueItem>> GetQueueItemsAsync(ulong guildId, int skip = 0,
        int take = 10)
    {
        var offset = GetState(guildId).CurrentItem is not null ? 1 : 0;
        return await queueRepository.GetPageAsync(guildId, skip + offset, take);
    }

    public async Task EnqueueItemsAsync(ulong guildId, IEnumerable<PlayQueueItem> items,
        IMessageChannel? feedbackChannel = null)
    {
        var state = GetState(guildId);

        if (feedbackChannel is not null)
        {
            state.FeedbackChannel = feedbackChannel;
        }

        var itemsList = items as IReadOnlyList<PlayQueueItem> ?? items.ToArray();
        await queueRepository.AddItemsAsync(guildId, itemsList);

        if (state is { IsPlaying: false, IsConnected: true })
        {
            await StartAsync(guildId);
        }
        else if (!state.IsPlaying)
        {
            _ = PrefetchTrackAsync(guildId, CancellationToken.None);
        }
    }

    public async Task<Result> ShuffleQueueAsync(ulong guildId)
    {
        var state = GetState(guildId);
        var currentItemId = state.CurrentItem?.Id;

        var count = await queueRepository.GetCountAsync(guildId);
        var shuffleableCount = currentItemId.HasValue ? count - 1 : count;
        if (shuffleableCount <= 1)
        {
            return Result.Failure("Not enough items in the queue to shuffle.");
        }

        await queueRepository.ShuffleAsync(guildId, excludeItemId: currentItemId);

        state.ClearPrefetchedTrack();
        _ = PrefetchTrackAsync(guildId, CancellationToken.None);

        return Result.Success();
    }

    public async Task ClearQueueAsync(ulong guildId)
    {
        var state = GetState(guildId);

        if (state.IsPlaying)
        {
            var loopTask = state.PlaybackLoopTask;
            await FullStopAsync(guildId);
            if (loopTask is not null)
            {
                await loopTask;
            }
        }

        state.ResetResumeState();
        state.ClearPrefetchedTrack();
        await queueRepository.ClearAsync(guildId);
        await ClearPersistedStateAsync(guildId);
    }

    public async Task StartAsync(ulong guildId, IMessageChannel? feedbackChannel = null)
    {
        var state = GetState(guildId);

        if (feedbackChannel is not null)
        {
            state.FeedbackChannel = feedbackChannel;
        }

        if (state.IsPlaying)
        {
            logger.LogInformation("Queue is already playing in guild {GuildId}", guildId);
            return;
        }

        var count = await queueRepository.GetCountAsync(guildId);
        if (count == 0)
        {
            logger.LogInformation("Queue is empty in guild {GuildId}, nothing to start", guildId);
            return;
        }

        state.PauseCts = new CancellationTokenSource();
        state.IsPlaying = true;

        logger.LogInformation("Starting queue playback in guild {GuildId}", guildId);

        state.PlaybackLoopTask = RunAdvancementLoopAsync(guildId);
    }

    public async Task PauseAsync(ulong guildId)
    {
        if (!_states.TryGetValue(guildId, out var state) || !state.IsPlaying)
        {
            logger.LogInformation("Queue is not playing in guild {GuildId}, nothing to stop", guildId);
            return;
        }

        logger.LogInformation("Pausing queue playback in guild {GuildId}", guildId);

        state.CancelPlayback();

        if (state.PlaybackLoopTask is { } loopTask)
        {
            state.PlaybackLoopTask = null;
            await loopTask;
        }

        await PersistStateAsync(guildId, state);

        state.ClearPrefetchedTrack();
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

        state.FullReset();
        await ClearPersistedStateAsync(guildId);
        await ClearActivityAsync();
    }

    public async Task<(PlayQueueItem? Skipped, PlayQueueItem? Next)> SkipAsync(ulong guildId)
    {
        var state = GetState(guildId);
        if (!state.IsPlaying)
        {
            logger.LogInformation("Queue is not playing in guild {GuildId}. Cannot skip", guildId);
            return (null, null);
        }

        logger.LogInformation("Skipping current track in guild {GuildId}", guildId);

        var currentItem = state.CurrentItem;
        var nextItem = state.PrefetchedTrack is not null
            ? await queueRepository.PeekNextAsync(guildId, skip: 1)
            : null;
        state.ResetResumeState();
        state.TriggerSkip();

        return (currentItem, nextItem);
    }

    public async Task GracefulStopAsync()
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
                await PauseAsync(guildId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to gracefully stop playback in guild {GuildId}", guildId);
            }
        }
    }

    public async Task RestoreAsync()
    {
        IReadOnlyList<PersistedGuildState> persisted;

        try
        {
            persisted = await stateRepository.GetAllAsync();
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
            await RestoreGuildAsync(saved);
        }
    }

    private async Task RestoreGuildAsync(PersistedGuildState saved)
    {
        var guildId = saved.GuildId;

        try
        {
            var guild = discordClient.GetGuild(guildId);
            if (guild is null)
            {
                logger.LogWarning("Guild {GuildId} not found during restore; clearing persisted state", guildId);
                await ClearPersistedStateAsync(guildId);
                return;
            }

            var voiceChannel = guild.GetVoiceChannel(saved.VoiceChannelId);
            if (voiceChannel is null)
            {
                logger.LogWarning(
                    "Voice channel {ChannelId} not found in guild {GuildId} during restore; clearing persisted state",
                    saved.VoiceChannelId, guildId);
                await ClearPersistedStateAsync(guildId);
                return;
            }

            var queueCount = await queueRepository.GetCountAsync(guildId);
            if (queueCount == 0)
            {
                logger.LogInformation("Queue is empty for guild {GuildId} during restore; clearing persisted state",
                    guildId);
                await ClearPersistedStateAsync(guildId);
                return;
            }

            var state = GetState(guildId);
            state.ResumePosition = saved.ResumePosition;
            state.ResumeItemId = saved.ResumeItemId;

            if (saved.FeedbackChannelId is { } feedbackId)
            {
                state.FeedbackChannel = guild.GetTextChannel(feedbackId);
            }

            logger.LogInformation(
                "Restoring guild {GuildId}: joining voice channel {ChannelId}, resume position {ResumePosition}",
                guildId, saved.VoiceChannelId, saved.ResumePosition);

            await voiceConnectionService.JoinAsync(voiceChannel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to restore playback state for guild {GuildId}", guildId);
            await ClearPersistedStateAsync(guildId);
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
                state.CurrentItem = await queueRepository.PeekNextAsync(guildId);
                var item = state.CurrentItem;

                if (item is null)
                {
                    logger.LogInformation("Queue is empty in guild {GuildId}. Auto-stopping playback.", guildId);
                    await FullStopAsync(guildId);
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
                    await queueRepository.DeleteByIdAsync(guildId, item.Id);
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

                await queueRepository.DeleteByIdAsync(guildId, item.Id);
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
            await ClearPersistedStateAsync(guildId);
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
        state.ResumeItemId = item.Id;
        state.ResumePosition = startFrom;

        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(pauseToken, skipToken);

        var streamResult = await AcquireOrUsePrefetchedStreamAsync(state, item, startFrom, streamCts.Token);
        if (!streamResult.IsSuccess)
        {
            await SendFeedbackAsync(guildId,
                $"Failed to play '{item.Title}'",
                $"{streamResult.ErrorMessage}. Skipping to next track.");
            return;
        }

        await using var pcmAudioStream = streamResult.Value!;

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

            int bytesRead;
            while ((bytesRead = await pcmAudioStream.Stream.ReadAsync(buffer, skipToken)) > 0)
            {
                await discordStream.WriteAsync(buffer.AsMemory(0, bytesRead), skipToken);
                bytesWritten += bytesRead;
                state.ResumePosition =
                    startFrom + TimeSpan.FromSeconds((double)bytesWritten / PcmBytesPerSecond);
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
        logger.LogInformation("Finished streaming '{Title}' in guild {GuildId}", item.Title, guildId);
    }

    private async Task<Result<PcmAudioStream>> AcquireOrUsePrefetchedStreamAsync(
        GuildPlaybackState state, PlayQueueItem item, TimeSpan startFrom, CancellationToken cancellationToken)
    {
        if (state.PrefetchedTrack is { } prefetched && prefetched.ItemId == item.Id && startFrom == TimeSpan.Zero)
        {
            var resolved = prefetched.ResolvedStream;
            state.ClearPrefetchedTrack();

            logger.LogInformation("Launching FFmpeg for prefetched '{Title}' in guild {GuildId}",
                item.Title, item.GuildId);

            return await LaunchAudioStreamAsync(item, resolved, TimeSpan.Zero, cancellationToken);
        }

        state.ClearPrefetchedTrack();
        return await AcquireAudioStreamAsync(item, startFrom, cancellationToken);
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

        var nextItem = await queueRepository.PeekNextAsync(guildId, skip: 1);
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

    private async Task PersistStateAsync(ulong guildId, GuildPlaybackState state)
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
                state.ResumeItemId));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist playback state for guild {GuildId}", guildId);
        }
    }

    private async Task ClearPersistedStateAsync(ulong guildId)
    {
        try
        {
            await stateRepository.DeleteAsync(guildId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear persisted playback state for guild {GuildId}", guildId);
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
