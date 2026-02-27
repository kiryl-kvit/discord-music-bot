using Discord;
using Discord.Interactions;
using DiscordMusicBot.Domain.Favorites;
using DiscordMusicBot.Domain.Playlists;

namespace DiscordMusicBot.App.Modules.Autocomplete;

public sealed class QueueAddAutocompleteHandler(
    IFavoriteRepository favoriteRepository,
    IPlaylistRepository playlistRepository) : AutocompleteHandler
{
    private const int MaxFavoriteResults = 15;
    private const int MaxPlaylistResults = 10;

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var userId = context.User.Id;
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        if (IsUrl(userInput))
        {
            return AutocompletionResult.FromSuccess();
        }

        var favorites = await FavoriteAutocompleteHelper.SearchAsync(favoriteRepository, userId, userInput);
        var playlists = await PlaylistAutocompleteHelper.SearchAsync(playlistRepository, userId, userInput);

        var favoriteResults = favorites.Take(MaxFavoriteResults).Select(f =>
            new AutocompleteResult(
                FavoriteAutocompleteHelper.FormatLabel(f),
                $"{FavoriteAutocompleteHelper.FavoritePrefix}{f.Id}"));

        var playlistResults = playlists.Take(MaxPlaylistResults).Select(p =>
            new AutocompleteResult(
                PlaylistAutocompleteHelper.FormatLabel(p),
                $"{PlaylistAutocompleteHelper.PlaylistPrefix}{p.Id}"));

        var results = favoriteResults.Concat(playlistResults)
            .Take(FavoriteAutocompleteHelper.MaxResults);

        return AutocompletionResult.FromSuccess(results);
    }

    private static bool IsUrl(string input)
    {
        return input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               input.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
