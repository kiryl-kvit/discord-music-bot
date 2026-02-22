using SpotifyAPI.Web;

namespace DiscordMusicBot.Core.MusicSource.Spotify;

public sealed record SpotifyTrack(string Title, string Artist, TimeSpan Duration)
{
    public static SpotifyTrack FromSimpleTrack(SimpleTrack track)
    {
        var artist = string.Join(", ", track.Artists.Select(a => a.Name));
        var duration = TimeSpan.FromMilliseconds(track.DurationMs);
        return new SpotifyTrack(track.Name, artist, duration);
    }
    
    public static SpotifyTrack FromFullTrack(FullTrack track)
    {
        var artist = string.Join(", ", track.Artists.Select(a => a.Name));
        var duration = TimeSpan.FromMilliseconds(track.DurationMs);
        return new SpotifyTrack(track.Name, artist, duration);
    }
}
