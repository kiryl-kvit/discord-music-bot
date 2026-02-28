using Discord;
using Discord.Interactions;
using DiscordMusicBot.Domain.History;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Modules.Autocomplete;

public sealed class HistoryAutocompleteHandler(IHistoryRepository historyRepository) : AutocompleteHandler
{
    private const int MaxResults = 25;
    private const int MaxLabelLength = 100;

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var guildId = context.Guild.Id;
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        var items = string.IsNullOrWhiteSpace(userInput)
            ? await historyRepository.GetPageAsync(guildId, skip: 0, take: MaxResults)
            : await historyRepository.SearchAsync(guildId, userInput, MaxResults);

        var results = items.Select(item =>
            new AutocompleteResult(FormatLabel(item), item.Id.ToString()));

        return AutocompletionResult.FromSuccess(results);
    }

    private static string FormatLabel(PlayQueueItem item)
    {
        var suffix = item.Author is not null ? $" - {item.Author}" : "";
        var label = $"{item.Title}{suffix}";

        return label.Length > MaxLabelLength ? label[..(MaxLabelLength - 3)] + "..." : label;
    }
}
