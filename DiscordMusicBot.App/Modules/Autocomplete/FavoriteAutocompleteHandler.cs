using Discord;
using Discord.Interactions;
using DiscordMusicBot.Domain.Favorites;

namespace DiscordMusicBot.App.Modules.Autocomplete;

public sealed class FavoriteAutocompleteHandler(IFavoriteRepository favoriteRepository) : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var userId = context.User.Id;
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        var favorites = await FavoriteAutocompleteHelper.SearchAsync(favoriteRepository, userId, userInput);

        var results = favorites.Select(f =>
            new AutocompleteResult(FavoriteAutocompleteHelper.FormatLabel(f), f.Id.ToString()));

        return AutocompletionResult.FromSuccess(results);
    }
}
