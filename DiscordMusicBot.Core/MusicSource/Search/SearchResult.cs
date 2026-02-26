namespace DiscordMusicBot.Core.MusicSource.Search;

public sealed record SearchResult(
    string Title,
    string Url,
    string? Author,
    TimeSpan? Duration,
    SearchResultKind Kind);
