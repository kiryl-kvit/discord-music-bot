using DiscordMusicBot.Core.MusicSource.Youtube;
using Microsoft.Extensions.Logging;
using YoutubeExplode;
using YoutubeExplode.Videos;

namespace DiscordMusicBot.Core.MusicSource.Related;

public sealed class YoutubeRelatedTrackProvider(
    YoutubeClient youtubeClient,
    ILogger<YoutubeRelatedTrackProvider> logger) : IRelatedTrackProvider
{
    private const string MixPlaylistPrefix = "RD";

    public async Task<MusicSource?> GetRelatedTrackAsync(string seedVideoUrl,
        IReadOnlyList<string> excludeUrls, CancellationToken cancellationToken = default)
    {
        var videoId = VideoId.TryParse(seedVideoUrl);
        if (videoId is null)
        {
            logger.LogWarning("Cannot extract video ID from URL '{Url}' for autoplay", seedVideoUrl);
            return null;
        }

        var mixPlaylistId = $"{MixPlaylistPrefix}{videoId.Value}";
        var excludeSet = new HashSet<string>(excludeUrls, StringComparer.OrdinalIgnoreCase);

        try
        {
            await foreach (var video in youtubeClient.Playlists.GetVideosAsync(mixPlaylistId, cancellationToken))
            {
                var url = YoutubeHelpers.VideoUrl(video.Id);

                if (excludeSet.Contains(url))
                {
                    continue;
                }

                var thumbnailUrl = video.Thumbnails.Count > 0
                    ? video.Thumbnails
                        .OrderBy(t => t.Resolution.Area)
                        .FirstOrDefault(t => t.Resolution.Width >= 120)?.Url ?? video.Thumbnails[0].Url
                    : null;

                return new MusicSource(SourceType.YouTube, video.Title, url, video.Author.ChannelTitle, video.Duration, thumbnailUrl);
            }

            logger.LogInformation(
                "No suitable related track found in mix playlist {MixId} (all excluded)", mixPlaylistId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch mix playlist {MixId} for autoplay", mixPlaylistId);
            return null;
        }
    }
}
