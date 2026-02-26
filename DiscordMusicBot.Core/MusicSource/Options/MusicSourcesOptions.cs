namespace DiscordMusicBot.Core.MusicSource.Options;

public sealed class MusicSourcesOptions
{
    public const string SectionName = "MusicSources";

    public int PlaylistLimit { get; init; } = 50;

    public double Volume { get; init; } = 1.0;

    public bool IsPlaylistLimitReached(int count) => PlaylistLimit > 0 && count >= PlaylistLimit;
}