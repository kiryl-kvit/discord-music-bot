using Discord;
using Discord.Interactions;
using DiscordMusicBot.Domain.Favorites;

namespace DiscordMusicBot.App.Modules.Autocomplete;

public sealed class QueueAddAutocompleteHandler(IFavoriteRepository favoriteRepository) : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var userId = context.User.Id;
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        if (IsUrl(userInput))
            return AutocompletionResult.FromSuccess();

        var favorites = await FavoriteAutocompleteHelper.SearchAsync(favoriteRepository, userId, userInput);

        var results = favorites.Select(f =>
            new AutocompleteResult(
                FavoriteAutocompleteHelper.FormatLabel(f),
                $"{FavoriteAutocompleteHelper.FavoritePrefix}{f.Id}"));

        return AutocompletionResult.FromSuccess(results);
    }

    private static bool IsUrl(string input)
    {
        return input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               input.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
