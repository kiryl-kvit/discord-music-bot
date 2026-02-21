using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordMusicBot.App.Extensions;
using DiscordMusicBot.App.Services.Models;
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
        return state.Items
            .Skip(skip)
            .Take(take)
            .ToArray();
    }

    public void EnqueueItems(ulong guildId, IEnumerable<PlayQueueItem> items)
    {
        var state = GetState(guildId);
        state.Items.AddRange(items);
    }

    public void Start(ulong guildId)
    {
        var state = GetState(guildId);

        if (state.IsPlaying)
        {
            logger.LogInformation("Queue is already playing in guild {GuildId}", guildId);
            return;
        }

        state.Cts = new CancellationTokenSource();
        state.IsPlaying = true;

        logger.LogInformation("Starting queue playback in guild {GuildId}", guildId);

        _ = RunAdvancementLoopAsync(guildId);
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

    private GuildPlaybackState GetState(ulong guildId)
    {
        return _states.GetOrAdd(guildId, new GuildPlaybackState());
    }

    private async Task RunAdvancementLoopAsync(ulong guildId)
    {
        var state = GetState(guildId);
        var cancellationToken = state.Cts!.Token;

        try
        {
            while (state.IsPlaying && !cancellationToken.IsCancellationRequested)
            {
                state.CurrentItem = state.Items.Pop();
                var item = state.CurrentItem;

                if (item is null)
                {
                    logger.LogInformation("Queue is empty in guild {GuildId}. Auto-stopping playback.", guildId);

                    CancelAndReset(state);
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

                var skipCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                state.SkipCts = skipCts;

                try
                {
                    await StreamToVoiceAsync(guildId);
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
            CancelAndReset(state);
            state.CurrentItem = null;
            await ClearActivityAsync();
        }
    }

    private async Task StreamToVoiceAsync(ulong guildId)
    {
        var state = GetState(guildId);
        var item = state.CurrentItem!;
        var cancellationToken = state.SkipCts?.Token ?? CancellationToken.None;

        var audioClient = voiceConnectionService.GetConnection(guildId)!;
        if (audioClient is null)
        {
            throw new ArgumentNullException(
                $"Not in voice channel for guild {guildId}. Cannot play tracks");
        }

        var provider = audioStreamProviderFactory.GetProvider(item.Url);

        var streamResult = await provider.GetAudioStreamAsync(item.Url, cancellationToken: cancellationToken);

        if (!streamResult.IsSuccess)
        {
            logger.LogWarning(
                "Failed to get audio stream for '{Title}' in guild {GuildId}: {Error}. Skipping.",
                item.Title, guildId, streamResult.ErrorMessage);
            return;
        }

        var pcmAudioStream = streamResult.Value!;

        await using (pcmAudioStream)
        {
            logger.LogInformation("Streaming audio for '{Title}' in guild {GuildId}", item.Title, guildId);

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
                int bytesRead;

                while ((bytesRead = await pcmAudioStream.Stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await discordStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
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

    private static void CancelAndReset(GuildPlaybackState state)
    {
        state.IsPlaying = false;

        if (state.DiscordPcmStream is not null)
        {
            var discordPcmStream = state.DiscordPcmStream;
            state.DiscordPcmStream = null;
            state.DiscordPcmStreamOwner = null;
            _ = DisposeDiscordPcmStreamAsync(discordPcmStream);
        }

        SafeCancelAndDispose(ref state.SkipCts);
        SafeCancelAndDispose(ref state.Cts);
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