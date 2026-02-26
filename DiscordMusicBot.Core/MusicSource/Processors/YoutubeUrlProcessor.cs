using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.Options;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using DiscordMusicBot.Core.MusicSource.Youtube;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace DiscordMusicBot.Core.MusicSource.Processors;

public sealed class YoutubeUrlProcessor(
    YoutubeClient youtubeClient,
    IOptionsMonitor<MusicSourcesOptions> options,
    ILogger<YoutubeUrlProcessor> logger)
    : IUrlProcessor
{

    public async Task<Result<MusicSourceResult>> GetMusicItemsAsync(string url,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedSources.TryGetSourceKey(url, out var key) ||
            !string.Equals(key, SupportedSources.YoutubeKey, StringComparison.OrdinalIgnoreCase))
        {
            return Result<MusicSourceResult>.Failure("Unsupported YouTube URL.");
        }

        var playlistId = PlaylistId.TryParse(url);
        if (playlistId is { } parsedPlaylistId)
        {
            return await GetPlaylistItemsAsync(parsedPlaylistId, cancellationToken);
        }

        var videoId = VideoId.TryParse(url);
        if (videoId is null)
        {
            return Result<MusicSourceResult>.Failure("Invalid YouTube URL.");
        }

        return await GetVideoItemAsync(videoId.Value, cancellationToken);
    }

    private async Task<Result<MusicSourceResult>> GetVideoItemAsync(VideoId videoId,
        CancellationToken cancellationToken)
    {
        try
        {
            var video = await youtubeClient.Videos.GetAsync(videoId, cancellationToken);
            var source = ToMusicSource(video);
            return Result<MusicSourceResult>.Success(new MusicSourceResult([source]));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve YouTube video {VideoId}.", videoId);
            return Result<MusicSourceResult>.Failure("Unable to fetch YouTube video metadata.");
        }
    }

    private async Task<Result<MusicSourceResult>> GetPlaylistItemsAsync(PlaylistId playlistId,
        CancellationToken cancellationToken)
    {
        var sources = new List<MusicSource>();
        string? playlistName = null;
        try
        {
            var playlist = await youtubeClient.Playlists.GetAsync(playlistId, cancellationToken);
            playlistName = playlist.Title;

            await foreach (var video in youtubeClient.Playlists.GetVideosAsync(playlistId, cancellationToken))
            {
                if (options.CurrentValue.IsPlaylistLimitReached(sources.Count))
                {
                    break;
                }

                if (TryConvertToMusicSource(video, out var source))
                {
                    sources.Add(source);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve YouTube playlist {PlaylistId}.", playlistId);

            if (sources.Count == 0)
            {
                return Result<MusicSourceResult>.Failure("Unable to fetch YouTube playlist metadata.");
            }

            logger.LogInformation("Partial playlist fetch: retrieved {Count} items before failure", sources.Count);
        }

        return Result<MusicSourceResult>.Success(new MusicSourceResult(sources, playlistName));
    }

    private bool TryConvertToMusicSource(IVideo video, out MusicSource source)
    {
        try
        {
            source = ToMusicSource(video);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skipping unavailable YouTube playlist item {VideoId}.", video.Id);
            source = null!;
            return false;
        }
    }

    private static MusicSource ToMusicSource(IVideo video)
    {
        var url = YoutubeHelpers.VideoUrl(video.Id);
        var author = video.Author?.ChannelTitle;
        var thumbnailUrl = GetThumbnailUrl(video.Thumbnails);
        return new MusicSource(video.Title, url, author, video.Duration, thumbnailUrl);
    }

    private static string? GetThumbnailUrl(IReadOnlyList<Thumbnail> thumbnails)
    {
        if (thumbnails.Count == 0)
        {
            return null;
        }

        return thumbnails
            .OrderBy(t => t.Resolution.Area)
            .FirstOrDefault(t => t.Resolution.Width >= 120)?.Url ?? thumbnails[0].Url;
    }
}