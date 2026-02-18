using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.Options;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace DiscordMusicBot.Core.MusicSource.Processors;

public sealed class YoutubeUrlProcessor(
    YoutubeClient youtubeClient,
    IOptions<MusicSourcesOptions> options,
    ILogger<YoutubeUrlProcessor> logger)
    : IUrlProcessor
{
    private readonly MusicSourcesOptions _options = options.Value;

    public async Task<Result<IReadOnlyCollection<MusicSource>>> GetMusicItemsAsync(string url,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedSources.TryGetSourceKey(url, out var key) ||
            !string.Equals(key, SupportedSources.YoutubeKey, StringComparison.OrdinalIgnoreCase))
        {
            return Result<IReadOnlyCollection<MusicSource>>.Failure("Unsupported YouTube URL.");
        }

        var playlistId = PlaylistId.TryParse(url);
        if (playlistId is { } parsedPlaylistId)
        {
            return await GetPlaylistItemsAsync(parsedPlaylistId, cancellationToken);
        }

        var videoId = VideoId.TryParse(url);
        if (videoId is null)
        {
            return Result<IReadOnlyCollection<MusicSource>>.Failure("Invalid YouTube URL.");
        }

        return await GetVideoItemAsync(videoId.Value, cancellationToken);
    }

    private async Task<Result<IReadOnlyCollection<MusicSource>>> GetVideoItemAsync(VideoId videoId,
        CancellationToken cancellationToken)
    {
        try
        {
            var video = await youtubeClient.Videos.GetAsync(videoId, cancellationToken);
            var source = ToMusicSource(video);
            return Result<IReadOnlyCollection<MusicSource>>.Success([source]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve YouTube video {VideoId}.", videoId);
            return Result<IReadOnlyCollection<MusicSource>>.Failure("Unable to fetch YouTube video metadata.");
        }
    }

    private async Task<Result<IReadOnlyCollection<MusicSource>>> GetPlaylistItemsAsync(PlaylistId playlistId,
        CancellationToken cancellationToken)
    {
        var sources = new List<MusicSource>();
        var limit = _options.PlaylistLimit;
        try
        {
            await foreach (var video in youtubeClient.Playlists.GetVideosAsync(playlistId, cancellationToken))
            {
                if (sources.Count >= limit)
                {
                    break;
                }

                try
                {
                    sources.Add(ToMusicSource(video));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipping unavailable YouTube playlist item {VideoId}.", video.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve YouTube playlist {PlaylistId}.", playlistId);
            if (sources.Count == 0)
            {
                return Result<IReadOnlyCollection<MusicSource>>.Failure("Unable to fetch YouTube playlist metadata.");
            }
        }

        return Result<IReadOnlyCollection<MusicSource>>.Success(sources);
    }

    private static MusicSource ToMusicSource(IVideo video)
    {
        var url = $"https://www.youtube.com/watch?v={video.Id}";
        var author = video.Author?.ChannelTitle;
        return new MusicSource(video.Title, url, author, video.Duration);
    }
}