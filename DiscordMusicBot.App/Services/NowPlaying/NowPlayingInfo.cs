using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services.NowPlaying;

public class NowPlayingInfo
{
    public required PlayQueueItem Item { get; init; }
    public required bool IsPaused { get; init; }
    public required TimeSpan Elapsed { get; init; }
    public PlayQueueItem? NextItem { get; init; }
    public int QueueCount { get; init; }
    public TimeSpan? QueueTotalDuration { get; init; }
    public bool IsAutoplayEnabled { get; init; }
}