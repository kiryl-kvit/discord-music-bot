namespace DiscordMusicBot.App.Options;

public sealed class FavoritesOptions
{
    public const string SectionName = "Favorites";

    public int Limit { get; init; } = 100;

    public bool IsLimitReached(int count) => Limit > 0 && count >= Limit;
}
