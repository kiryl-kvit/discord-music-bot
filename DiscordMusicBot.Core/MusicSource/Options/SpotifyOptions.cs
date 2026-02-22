namespace DiscordMusicBot.Core.MusicSource.Options;

public sealed class SpotifyOptions
{
    public const string SectionName = "Spotify";

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;
}
