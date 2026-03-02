using Discord;

namespace DiscordMusicBot.App.Services.NowPlaying;

internal sealed class NowPlayingMessageState
{
    public ulong ChannelId { get; init; }
    public ulong MessageId { get; init; }
    public required IMessageChannel Channel { get; init; }
    public DateTimeOffset LastEditUtc { get; set; }
    public CancellationTokenSource? TimerCts;
}