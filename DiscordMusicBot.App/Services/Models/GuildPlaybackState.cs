using Discord;
using Discord.Audio;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services.Models;

public sealed class GuildPlaybackState
{
    public PlayQueueItem? CurrentItem;

    public volatile bool IsPlaying;
    public volatile bool IsConnected;

    public ulong? VoiceChannelId;

    public IAudioClient? DiscordPcmStreamOwner;
    public AudioOutStream? DiscordPcmStream;

    public CancellationTokenSource? PauseCts;
    public CancellationTokenSource? SkipCts;

    public Task? PlaybackLoopTask;

    public TimeSpan ResumePosition;
    public long? ResumeItemId;

    public PlaybackTrack? PrefetchedTrack;

    public IMessageChannel? FeedbackChannel;

    public void ResetResumeState()
    {
        ResumePosition = TimeSpan.Zero;
        ResumeItemId = null;
    }

    public void ClearPrefetchedTrack()
    {
        PrefetchedTrack = null;
    }

    public void TriggerSkip()
    {
        try
        {
            SkipCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }
    }

    public void CancelPlayback()
    {
        IsPlaying = false;

        if (DiscordPcmStream is not null)
        {
            var discordPcmStream = DiscordPcmStream;
            DiscordPcmStream = null;
            DiscordPcmStreamOwner = null;
            _ = DisposeDiscordPcmStreamAsync(discordPcmStream);
        }

        SafeCancelAndDispose(ref PauseCts);
        SafeCancelAndDispose(ref SkipCts);
    }

    public void FullReset()
    {
        ResetResumeState();
        CancelPlayback();
        PlaybackLoopTask = null;
        CurrentItem = null;
        ClearPrefetchedTrack();
    }

    public void ResetDiscordStream(AudioOutStream failedStream)
    {
        DiscordPcmStream = null;
        DiscordPcmStreamOwner = null;
        _ = DisposeDiscordPcmStreamAsync(failedStream);
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
}
