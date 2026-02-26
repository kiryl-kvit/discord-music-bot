using SpotifyAPI.Web;

namespace DiscordMusicBot.Core.MusicSource.Spotify;

public sealed record SpotifyTrack(string Title, string Artist, TimeSpan Duration, string? ImageUrl = null)
{
    public static SpotifyTrack FromSimpleTrack(SimpleTrack track, string? imageUrl = null) =>
        Create(track.Name, track.Artists.Select(a => a.Name), track.DurationMs, imageUrl);

    public static SpotifyTrack FromFullTrack(FullTrack track) =>
        Create(track.Name, track.Artists.Select(a => a.Name), track.DurationMs,
            track.Album?.Images?.FirstOrDefault()?.Url);

    private static SpotifyTrack Create(string name, IEnumerable<string> artistNames, int durationMs,
        string? imageUrl)
    {
        var artist = string.Join(", ", artistNames);
        var duration = TimeSpan.FromMilliseconds(durationMs);
        return new SpotifyTrack(name, artist, duration, imageUrl);
    }
}
