using Discord;

namespace DiscordMusicBot.App.Services.Models;

public sealed class NowPlayingState
{
    public ulong ChannelId { get; init; }
    public ulong MessageId { get; set; }
    public IUserMessage? Message { get; set; }
    public CancellationTokenSource TimerCts { get; set; } = new();
}