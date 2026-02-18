namespace DiscordMusicBot.Core.MusicSource.Options;

public sealed class MusicSourcesOptions
{
    public const string SectionName = "MusicSources";

    public int PlaylistLimit { get; init; } = 50;
}
