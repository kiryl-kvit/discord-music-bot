namespace DiscordMusicBot.Core.MusicSource;

public sealed record MusicSourceResult(IReadOnlyCollection<MusicSource> Items, string? PlaylistName = null);
