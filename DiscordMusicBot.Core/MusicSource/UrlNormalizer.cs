using System.Text.RegularExpressions;
using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.Suno;
using DiscordMusicBot.Core.MusicSource.Youtube;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace DiscordMusicBot.Core.MusicSource;

public static partial class UrlNormalizer
{
    [GeneratedRegex(@"open\.spotify\.com/(?<type>track|playlist|album)/(?<id>[a-zA-Z0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SpotifyUrlPattern();

    [GeneratedRegex(@"suno\.com/(?<type>song|playlist)/(?<id>[0-9a-fA-F-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SunoUrlPattern();

    /// <summary>
    /// Normalizes a URL to its canonical form for the appropriate music source.
    /// Returns null if the URL doesn't match any known source pattern.
    /// </summary>
    public static string? TryNormalize(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!SupportedSources.TryGetSourceType(url, out var sourceType))
        {
            return null;
        }

        return sourceType switch
        {
            SourceType.YouTube => NormalizeYoutube(url),
            SourceType.Spotify => NormalizeSpotify(url),
            SourceType.Suno => NormalizeSuno(url),
            _ => null,
        };
    }

    private static string? NormalizeYoutube(string url)
    {
        // Check playlist first (same priority as YoutubeUrlProcessor)
        var playlistId = PlaylistId.TryParse(url);
        if (playlistId is not null)
        {
            return $"https://www.youtube.com/playlist?list={playlistId}";
        }

        var videoId = VideoId.TryParse(url);
        if (videoId is not null)
        {
            return YoutubeHelpers.VideoUrl(videoId.Value);
        }

        return null;
    }

    private static string? NormalizeSpotify(string url)
    {
        var match = SpotifyUrlPattern().Match(url);
        if (!match.Success)
        {
            return null;
        }

        var type = match.Groups["type"].Value.ToLowerInvariant();
        var id = match.Groups["id"].Value;
        return $"https://open.spotify.com/{type}/{id}";
    }

    private static string? NormalizeSuno(string url)
    {
        var match = SunoUrlPattern().Match(url);
        if (!match.Success)
        {
            return null;
        }

        var type = match.Groups["type"].Value.ToLowerInvariant();
        var id = match.Groups["id"].Value;

        return type switch
        {
            "song" => SunoTrack.GetSongPageUrl(id),
            "playlist" => SunoTrack.GetPlaylistPageUrl(id),
            _ => null,
        };
    }
}
