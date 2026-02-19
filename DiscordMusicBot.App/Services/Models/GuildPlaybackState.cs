using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services.Models;

public sealed class GuildPlaybackState
{
    public volatile bool IsPlaying;
    public PlayQueueItem? CurrentItem;
    public CancellationTokenSource? Cts;
}