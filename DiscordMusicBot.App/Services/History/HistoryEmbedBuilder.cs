using Discord;
using DiscordMusicBot.Core.Formatters;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services.History;

public static class HistoryEmbedBuilder
{
    public const int PageSize = 10;

    public static Embed BuildListEmbed(IReadOnlyList<PlayQueueItem> items, int page, int pageSize, int totalCount)
    {
        var builder = new EmbedBuilder()
            .WithTitle("History")
            .WithColor(Color.DarkGrey)
            .WithFooter($"Page {page} | {totalCount} total played");

        if (items.Count == 0)
        {
            builder.WithDescription("No history yet. Tracks will appear here after they finish playing.");
            return builder.Build();
        }

        var lines = items.Select((item, index) => FormatLine(item, page, pageSize, index));
        builder.WithDescription(string.Join('\n', lines));

        return builder.Build();
    }

    private static string FormatLine(PlayQueueItem item, int page, int pageSize, int index)
    {
        var position = (page - 1) * pageSize + index + 1;
        var duration = DateFormatter.FormatTimeOrDefault(item.Duration);
        return $"`{position}.` **{item.Title}** - {DisplayConstants.AuthorOrDefault(item.Author)} `[{duration}]`";
    }

    public static MessageComponent BuildPageControls(int page, bool hasNextPage)
    {
        return new ComponentBuilder()
            .WithButton("Prev", $"history:page:{page - 1}", ButtonStyle.Secondary,
                new Emoji("\u25C0\uFE0F"), disabled: page <= 1)
            .WithButton("Next", $"history:page:{page + 1}", ButtonStyle.Secondary,
                new Emoji("\u25B6\uFE0F"), disabled: !hasNextPage)
            .Build();
    }
}
