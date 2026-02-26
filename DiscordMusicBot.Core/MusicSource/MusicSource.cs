namespace DiscordMusicBot.Core.MusicSource;

public sealed record MusicSource(
    string Title,
    string Url,
    string? Author,
    TimeSpan? Duration,
    string? ThumbnailUrl = null);