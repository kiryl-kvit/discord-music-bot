using DiscordMusicBot.Core.MusicSource.AudioStreaming;

namespace DiscordMusicBot.App.Services.Models;

public sealed class PlaybackTrack
{
    public required long ItemId { get; init; }
    public required ResolvedStream ResolvedStream { get; init; }
}
