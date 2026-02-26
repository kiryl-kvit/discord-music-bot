using DiscordMusicBot.Domain.Favorites;

namespace DiscordMusicBot.App.Modules.Autocomplete;

public static class FavoriteAutocompleteHelper
{
    public const int MaxResults = 25;
    public const string FavoritePrefix = "fav:";
    private const int MaxLabelLength = 100;

    public static async Task<IReadOnlyList<FavoriteItem>> SearchAsync(
        IFavoriteRepository repository, ulong userId, string input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? await repository.GetByUserAsync(userId, skip: 0, take: MaxResults)
            : await repository.SearchAsync(userId, input, MaxResults);
    }

    public static string FormatLabel(FavoriteItem item)
    {
        var name = item.DisplayName;
        var suffix = item.IsPlaylist
            ? " [Playlist]"
            : item.Author is not null
                ? $" - {item.Author}"
                : "";

        var label = $"{name}{suffix}";

        return label.Length > MaxLabelLength ? label[..(MaxLabelLength - 3)] + "..." : label;
    }
}
