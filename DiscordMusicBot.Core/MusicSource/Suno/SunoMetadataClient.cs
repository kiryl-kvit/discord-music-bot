using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.Core.MusicSource.Suno;

public sealed partial class SunoMetadataClient(
    HttpClient httpClient,
    ILogger<SunoMetadataClient> logger)
{
    [GeneratedRegex(@"<title>(.+?)\s+by\s+(.+?)\s*\|\s*Suno</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleTagPattern();

    [GeneratedRegex(@"<meta\s+(?:property|name)=""og:title""\s+content=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgTitlePattern();

    [GeneratedRegex(@"<meta\s+(?:property|name)=""og:audio""\s+content=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgAudioPattern();

    [GeneratedRegex(@"title=""([^""]+)"">\s*<a\s+href=""/song/([a-f0-9-]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PlaylistSongPattern();

    [GeneratedRegex(@"cdn1\.suno\.ai/([a-f0-9-]+)\.mp3", RegexOptions.IgnoreCase)]
    private static partial Regex CdnSongIdPattern();

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

    public async Task<IReadOnlyList<SunoTrack>> GetPlaylistTracksAsync(string playlistId,
        int limit, CancellationToken cancellationToken)
    {
        try
        {
            var url = SunoTrack.GetPlaylistPageUrl(playlistId);
            var html = await FetchHtmlAsync(url, cancellationToken);

            return string.IsNullOrEmpty(html) ? [] : ParsePlaylistPage(html, limit);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch Suno playlist metadata for {PlaylistId}.", playlistId);
            return [];
        }
    }

    private SunoTrack? ParseSongPage(string songId, string html)
    {
        // <title> format: "{Title} by {Artist} | Suno"
        var titleMatch = TitleTagPattern().Match(html);
        if (titleMatch.Success)
        {
            var title = titleMatch.Groups[1].Value.Trim();
            var artist = titleMatch.Groups[2].Value.Trim();
            return new SunoTrack(title, artist, songId);
        }

        // Fallback: use og:title (title only, no artist)
        var ogTitleMatch = OgTitlePattern().Match(html);
        if (ogTitleMatch.Success)
        {
            var title = ogTitleMatch.Groups[1].Value.Trim();
            return new SunoTrack(title, Artist: null, songId);
        }

        logger.LogWarning("Could not parse metadata from Suno song page for {SongId}.", songId);
        return null;
    }

    private static List<SunoTrack> ParsePlaylistPage(string html, int limit)
    {
        var titlesBySongId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            if (tracks.Count >= limit)
            {
                break;
            }

            var title = titlesBySongId.GetValueOrDefault(songId) ?? songId;
            tracks.Add(new SunoTrack(title, Artist: null, songId));
        }

        if (tracks.Count != 0)
        {
            return tracks;
        }

        // If og:audio tags were missing, fall back to song links from the HTML body.
        foreach (var (songId, title) in titlesBySongId)
        {
            if (tracks.Count >= limit)
            {
                break;
            }

            tracks.Add(new SunoTrack(title, Artist: null, songId));
        }

        return tracks;
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