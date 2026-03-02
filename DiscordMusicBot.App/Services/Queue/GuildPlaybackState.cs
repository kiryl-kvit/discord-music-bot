using Discord;
using Discord.Audio;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services.Queue;

public sealed class GuildPlaybackState
{
    public PlayQueueItem? CurrentItem;

    public volatile bool IsPlaying;
    public volatile bool IsConnected;
    public volatile bool IsReconnecting;
    private int _autoplayFillInProgress;

    public bool TryBeginAutoplayFill() =>
        Interlocked.CompareExchange(ref _autoplayFillInProgress, 1, 0) == 0;

    public void EndAutoplayFill() =>
        Interlocked.Exchange(ref _autoplayFillInProgress, 0);

    public ulong? VoiceChannelId;

    public IAudioClient? DiscordPcmStreamOwner;
    public AudioOutStream? DiscordPcmStream;

    public CancellationTokenSource? PauseCts;
    public CancellationTokenSource? SkipCts;

    public Task? PlaybackLoopTask;

    public TimeSpan ResumePosition;
    public long? ResumeItemId;

    public PlaybackTrack? PrefetchedTrack;
    public PlaybackTrack? CurrentTrack;
    private long _prefetchVersion;

    public IMessageChannel? FeedbackChannel;

    public DateTimeOffset? PlaybackStartedUtc;
    public TimeSpan PlaybackStartOffset;

    public TimeSpan GetElapsedTime()
    {
        if (PlaybackStartedUtc is null)
        {
            return PlaybackStartOffset;
        }

        return PlaybackStartOffset + (DateTimeOffset.UtcNow - PlaybackStartedUtc.Value);
    }

    public void ResetResumeState()
    {
        ResumePosition = TimeSpan.Zero;
        ResumeItemId = null;
    }

    public void ClearPrefetchedTrack()
    {
        PrefetchedTrack = null;
        Interlocked.Increment(ref _prefetchVersion);
    }

    public long PrefetchVersion => Interlocked.Read(ref _prefetchVersion);

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
        ResetElapsedTime();
        CancelPlayback();
        PlaybackLoopTask = null;
        CurrentItem = null;
        ClearPrefetchedTrack();
        CurrentTrack = null;
    }

    public void ResetElapsedTime()
    {
        PlaybackStartedUtc = null;
        PlaybackStartOffset = TimeSpan.Zero;
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
