using Discord.Audio;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services.Models;

public sealed class GuildPlaybackState
{
    public List<PlayQueueItem> Items { get; set; } = [];
    public PlayQueueItem? CurrentItem;
    
    public PlaybackTrack? CurrentTrack;
    public PlaybackTrack? NextTrack;
    
    public volatile bool IsPlaying;
    public volatile bool IsConnected;
    
    public IAudioClient? DiscordPcmStreamOwner;
    public AudioOutStream? DiscordPcmStream;
    
    public CancellationTokenSource? Cts;
    public CancellationTokenSource? SkipCts;
}