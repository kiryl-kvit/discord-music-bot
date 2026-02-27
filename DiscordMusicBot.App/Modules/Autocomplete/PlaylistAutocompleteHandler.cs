using Discord;
using Discord.Interactions;
using DiscordMusicBot.Domain.Playlists;

namespace DiscordMusicBot.App.Modules.Autocomplete;

public sealed class PlaylistAutocompleteHandler(IPlaylistRepository playlistRepository) : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var userId = context.User.Id;
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        var playlists = await PlaylistAutocompleteHelper.SearchAsync(playlistRepository, userId, userInput);

        var results = playlists.Select(p =>
            new AutocompleteResult(PlaylistAutocompleteHelper.FormatLabel(p), p.Id.ToString()));

        return AutocompletionResult.FromSuccess(results);
    }
}
