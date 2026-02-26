using System.Text.RegularExpressions;

namespace DiscordMusicBot.Core.MusicSource.Suno;

public sealed partial record SunoTrack(string Title, string? Artist, string SongId, string? ImageUrl = null)
{
    private const string BaseUrl = "https://suno.com";
    private const string CdnBaseUrl = "https://cdn1.suno.ai";

    [GeneratedRegex(@"suno\.com/song/(?<id>[0-9a-fA-F-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SongUrlPattern();

    public string CdnUrl => GetCdnUrl(SongId);

    public string SongUrl => GetSongPageUrl(SongId);

    public static string GetCdnUrl(string songId) => $"{CdnBaseUrl}/{songId}.mp3";

    public static string GetSongPageUrl(string songId) => $"{BaseUrl}/song/{songId}";

    public static string GetPlaylistPageUrl(string playlistId) => $"{BaseUrl}/playlist/{playlistId}";

    public static string? ExtractSongId(string url)
    {
        var match = SongUrlPattern().Match(url);
        return match.Success ? match.Groups["id"].Value : null;
    }
}
