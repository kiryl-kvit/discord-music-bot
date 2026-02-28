using System.Globalization;
using System.Text.RegularExpressions;
using DiscordMusicBot.Core.MusicSource.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusicBot.Core.MusicSource.Suno;

public sealed partial class SunoMetadataClient(
    HttpClient httpClient,
    IOptionsMonitor<MusicSourcesOptions> options,
    ILogger<SunoMetadataClient> logger)
{
    [GeneratedRegex(@"<title>(.+?)\s+by\s+(.+?)\s*\|\s*Suno</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleTagPattern();

    [GeneratedRegex(@"<meta\s+(?:property|name)=""og:title""\s+content=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgTitlePattern();

    [GeneratedRegex(@"<meta\s+(?:property|name)=""og:audio""\s+content=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgAudioPattern();

    [GeneratedRegex(@"<meta\s+(?:property|name)=""og:image""\s+content=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgImagePattern();

    [GeneratedRegex(@"title=""([^""]+)"">\s*<a\s+href=""/song/([a-f0-9-]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PlaylistSongPattern();

    [GeneratedRegex(@"cdn1\.suno\.ai/([a-f0-9-]+)\.mp3", RegexOptions.IgnoreCase)]
    private static partial Regex CdnSongIdPattern();

    [GeneratedRegex(@"\\""duration\\"":([\d.]+)")]
    private static partial Regex DurationPattern();

    [GeneratedRegex(@"\\""id\\"":\\""([a-f0-9-]+)\\"",\\""entity_type\\"":\\""song_schema\\""")]
    private static partial Regex ClipIdPattern();

    public async Task<SunoTrack?> GetSongAsync(string songId, CancellationToken cancellationToken)
    {
        try
        {
            var url = SunoTrack.GetSongPageUrl(songId);
            var html = await FetchHtmlAsync(url, cancellationToken);

            return string.IsNullOrEmpty(html) ? null : ParseSongPage(songId, html);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch Suno song metadata for {SongId}.", songId);
            return null;
        }
    }

    public async Task<(string? PlaylistName, IReadOnlyList<SunoTrack> Tracks)> GetPlaylistAsync(string playlistId,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = SunoTrack.GetPlaylistPageUrl(playlistId);
            var html = await FetchHtmlAsync(url, cancellationToken);

            if (string.IsNullOrEmpty(html))
            {
                return (null, []);
            }

            var playlistName = ParseOgTitle(html);
            var tracks = ParsePlaylistPage(html);
            return (playlistName, tracks);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch Suno playlist metadata for {PlaylistId}.", playlistId);
            return (null, []);
        }
    }

    private SunoTrack? ParseSongPage(string songId, string html)
    {
        var imageUrl = ParseOgImage(html);
        var duration = ParseDuration(html);

        // <title> format: "{Title} by {Artist} | Suno"
        var titleMatch = TitleTagPattern().Match(html);
        if (titleMatch.Success)
        {
            var title = titleMatch.Groups[1].Value.Trim();
            var artist = titleMatch.Groups[2].Value.Trim();
            return new SunoTrack(title, artist, songId, duration, imageUrl);
        }

        // Fallback: use og:title (title only, no artist)
        var ogTitleMatch = OgTitlePattern().Match(html);
        if (ogTitleMatch.Success)
        {
            var title = ogTitleMatch.Groups[1].Value.Trim();
            return new SunoTrack(title, Artist: null, songId, duration, imageUrl);
        }

        logger.LogWarning("Could not parse metadata from Suno song page for {SongId}.", songId);
        return null;
    }

    private List<SunoTrack> ParsePlaylistPage(string html)
    {
        var titlesBySongId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var durationsBySongId = ParseDurationsBySongId(html);

        foreach (Match match in PlaylistSongPattern().Matches(html))
        {
            var title = match.Groups[1].Value.Trim();
            var songId = match.Groups[2].Value;

            titlesBySongId.TryAdd(songId, title);
        }

        var audioSongIds = new List<string>();

        foreach (Match match in OgAudioPattern().Matches(html))
        {
            var cdnUrl = match.Groups[1].Value;
            var cdnMatch = CdnSongIdPattern().Match(cdnUrl);
            if (cdnMatch.Success)
            {
                audioSongIds.Add(cdnMatch.Groups[1].Value);
            }
        }

        var tracks = new List<SunoTrack>();

        foreach (var songId in audioSongIds)
        {
            if (options.CurrentValue.IsPlaylistLimitReached(tracks.Count))
            {
                break;
            }

            var title = titlesBySongId.GetValueOrDefault(songId) ?? songId;
            durationsBySongId.TryGetValue(songId, out var duration);
            tracks.Add(new SunoTrack(title, Artist: null, songId, duration));
        }

        if (tracks.Count != 0)
        {
            return tracks;
        }

        // If og:audio tags were missing, fall back to song links from the HTML body.
        foreach (var (songId, title) in titlesBySongId)
        {
            if (options.CurrentValue.IsPlaylistLimitReached(tracks.Count))
            {
                break;
            }

            durationsBySongId.TryGetValue(songId, out var duration);
            tracks.Add(new SunoTrack(title, Artist: null, songId, duration));
        }

        return tracks;
    }

    private static string? ParseOgTitle(string html)
    {
        var match = OgTitlePattern().Match(html);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ParseOgImage(string html)
    {
        var match = OgImagePattern().Match(html);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static TimeSpan? ParseDuration(string html)
    {
        var match = DurationPattern().Match(html);
        if (match.Success &&
            double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var seconds) &&
            seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }

    private static Dictionary<string, TimeSpan> ParseDurationsBySongId(string html)
    {
        var result = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);

        var clipIds = ClipIdPattern().Matches(html);
        var durations = DurationPattern().Matches(html);

        if (clipIds.Count == 0 || clipIds.Count != durations.Count)
        {
            return result;
        }

        for (var i = 0; i < clipIds.Count; i++)
        {
            var songId = clipIds[i].Groups[1].Value;
            if (double.TryParse(durations[i].Groups[1].Value, CultureInfo.InvariantCulture, out var seconds) &&
                seconds > 0)
            {
                result.TryAdd(songId, TimeSpan.FromSeconds(seconds));
            }
        }

        return result;
    }

    private async Task<string?> FetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "text/html");

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        logger.LogWarning("Suno returned HTTP {StatusCode} for {Url}.", (int)response.StatusCode, url);
        return null;
    }
}