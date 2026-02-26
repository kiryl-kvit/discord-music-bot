using Discord;
using DiscordMusicBot.Core.Formatters;
using DiscordMusicBot.Core.MusicSource.Search;

namespace DiscordMusicBot.App.Services;

public static class SearchEmbedBuilder
{
    public const int MaxResults = 5;
    public const string SelectMenuId = "search:select";

    private const string PlaylistLabel = "Playlist";

    public static Embed BuildResultsEmbed(string query, IReadOnlyList<SearchResult> results)
    {
        var builder = new EmbedBuilder()
            .WithTitle($"Search Results for: {Truncate(query, 230)}")
            .WithColor(Color.Blue);

        var lines = results.Select(FormatLine);
        builder.WithDescription(string.Join('\n', lines));

        return builder.Build();
    }

    public static MessageComponent BuildSelectMenu(IReadOnlyList<SearchResult> results)
    {
        var menuBuilder = new SelectMenuBuilder()
            .WithCustomId(SelectMenuId)
            .WithPlaceholder("Select a track or playlist to enqueue")
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (var result in results)
        {
            AddOption(menuBuilder, result);
        }

        return new ComponentBuilder()
            .WithSelectMenu(menuBuilder)
            .Build();
    }

    public static MessageComponent BuildDisabledSelectMenu(string selectedTitle)
    {
        var menuBuilder = new SelectMenuBuilder()
            .WithCustomId(SelectMenuId)
            .WithPlaceholder(Truncate($"Selected: {selectedTitle}", 150))
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithDisabled(true)
            .AddOption("\u2014", "disabled");

        return new ComponentBuilder()
            .WithSelectMenu(menuBuilder)
            .Build();
    }

    private static string FormatLine(SearchResult result, int index)
    {
        var tag = FormatTag(result);
        return $"`{index + 1}.` **{result.Title}** - {result.Author ?? DisplayConstants.UnknownAuthor} `[{tag}]`";
    }

    private static void AddOption(SelectMenuBuilder menuBuilder, SearchResult result)
    {
        var tag = FormatTag(result);
        var description = $"{result.Author ?? DisplayConstants.UnknownAuthor} [{tag}]";

        menuBuilder.AddOption(
            Truncate(result.Title, 100),
            result.Url,
            Truncate(description, 100));
    }

    private static string FormatTag(SearchResult result) => result.Kind switch
    {
        SearchResultKind.Track when result.Duration is not null => DateFormatter.FormatTime(result.Duration.Value),
        SearchResultKind.Track => DisplayConstants.UnknownDuration,
        SearchResultKind.Playlist => PlaylistLabel,
        _ => DisplayConstants.UnknownDuration,
    };

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "\u2026");
    }
}
