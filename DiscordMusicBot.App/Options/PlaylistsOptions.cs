namespace DiscordMusicBot.App.Options;

public sealed class PlaylistsOptions
{
    public const string SectionName = "Playlists";

    public int Limit { get; init; } = 25;

    public int ItemLimit { get; init; } = 200;

    public bool IsLimitReached(int count) => Limit > 0 && count >= Limit;
}
