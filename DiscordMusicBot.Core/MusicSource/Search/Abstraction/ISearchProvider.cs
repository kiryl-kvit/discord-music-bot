namespace DiscordMusicBot.Core.MusicSource.Search.Abstraction;

public interface ISearchProvider
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int maxResults,
        CancellationToken cancellationToken = default);
}
