using SpotifyAPI.Web;

namespace DiscordMusicBot.Core.MusicSource.Spotify;

public sealed record SpotifyTrack(string Title, string Artist, TimeSpan Duration)
{
    public static SpotifyTrack FromSimpleTrack(SimpleTrack track) =>
        Create(track.Name, track.Artists.Select(a => a.Name), track.DurationMs);

    public static SpotifyTrack FromFullTrack(FullTrack track) =>
        Create(track.Name, track.Artists.Select(a => a.Name), track.DurationMs);

    private static SpotifyTrack Create(string name, IEnumerable<string> artistNames, int durationMs)
    {
        var artist = string.Join(", ", artistNames);
        var duration = TimeSpan.FromMilliseconds(durationMs);
        return new SpotifyTrack(name, artist, duration);
    }
}
