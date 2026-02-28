namespace DiscordMusicBot.Core.MusicSource;

public sealed record MusicSource(
    SourceType SourceType,
    string Title,
    string Url,
    string? Author,
    TimeSpan? Duration,
    string? ThumbnailUrl = null);