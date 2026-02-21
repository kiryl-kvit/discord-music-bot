using Discord;
using Discord.Audio;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services.Models;

public sealed class GuildPlaybackState
{
    private readonly Lock _itemsLock = new();
    private readonly List<PlayQueueItem> _items = [];

    public PlayQueueItem? CurrentItem;

    public volatile bool IsPlaying;
    public volatile bool IsConnected;

    public IAudioClient? DiscordPcmStreamOwner;
    public AudioOutStream? DiscordPcmStream;

    public CancellationTokenSource? PauseCts;
    public CancellationTokenSource? SkipCts;

    public TimeSpan ResumePosition;
    public long? ResumeItemId;

    public PlaybackTrack? PrefetchedTrack;

    public IMessageChannel? FeedbackChannel;

    public T WithItems<T>(Func<List<PlayQueueItem>, T> action)
    {
        lock (_itemsLock)
        {
            return action(_items);
        }
    }

    public void WithItems(Action<List<PlayQueueItem>> action)
    {
        lock (_itemsLock)
        {
            action(_items);
        }
    }
}