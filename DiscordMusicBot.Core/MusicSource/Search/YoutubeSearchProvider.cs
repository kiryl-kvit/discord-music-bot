using DiscordMusicBot.Core.MusicSource.Search.Abstraction;
using DiscordMusicBot.Core.MusicSource.Youtube;
using Microsoft.Extensions.Logging;
using YoutubeExplode;
using YoutubeExplode.Search;

namespace DiscordMusicBot.Core.MusicSource.Search;

public sealed class YoutubeSearchProvider(
    YoutubeClient youtubeClient,
    ILogger<YoutubeSearchProvider> logger) : ISearchProvider
{
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int maxResults,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await youtubeClient.Search.GetResultsAsync(query, cancellationToken)
                .Where(r => r is VideoSearchResult or PlaylistSearchResult)
                .Take(maxResults)
                .Select(MapToSearchResult)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "YouTube search failed for query \"{Query}\".", query);
            return [];
        }
    }

    private static SearchResult MapToSearchResult(ISearchResult result) => result switch
    {
        VideoSearchResult video => new SearchResult(
            video.Title,
            YoutubeHelpers.VideoUrl(video.Id),
            video.Author.ChannelTitle,
            video.Duration,
            SearchResultKind.Track),

        PlaylistSearchResult playlist => new SearchResult(
            playlist.Title,
            playlist.Url,
            playlist.Author?.ChannelTitle,
            null,
            SearchResultKind.Playlist),

        _ => throw new ArgumentOutOfRangeException(nameof(result), result.GetType().Name, null),
    };
}
