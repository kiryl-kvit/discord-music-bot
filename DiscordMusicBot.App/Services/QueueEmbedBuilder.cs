using Discord;
using DiscordMusicBot.Core.Formatters;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services;

public static class QueueEmbedBuilder
{
    public const int PageSize = 10;
    private const string UnknownAuthor = "Unknown";
    private const string UnknownDuration = "??:??";

    public static int CalculateTotalPages(int itemCount)
    {
        return Math.Max(1, (int)Math.Ceiling(itemCount / (double)PageSize));
    }

    public static Embed BuildQueueEmbed(IReadOnlyList<PlayQueueItem> items, PlayQueueItem? currentItem,
        int page, int totalPages)
    {
        var builder = new EmbedBuilder()
            .WithTitle("Queue")
            .WithColor(Color.Teal);

        if (currentItem is not null)
        {
            builder.WithDescription($"**Now playing:** {FormatNowPlaying(currentItem)}");
        }

        if (items.Count == 0)
        {
            builder.AddField("Up Next", "Queue is empty.");
        }
        else
        {
            var lines = items.Select((item, index) => FormatQueueLine(item, page, index));
            builder.AddField("Up Next", string.Join('\n', lines));
        }

        if (totalPages > 1)
        {
            builder.WithFooter($"Page {page + 1} of {totalPages}");
        }

        return builder.Build();
    }

    private static string FormatNowPlaying(PlayQueueItem item)
    {
        return $"{item.Title} - {item.Author ?? UnknownAuthor}";
    }

    private static string FormatQueueLine(PlayQueueItem item, int page, int index)
    {
        var position = page * PageSize + index + 1;
        var duration = item.Duration is not null
            ? DateFormatter.FormatTime(item.Duration.Value)
            : UnknownDuration;
        return $"`{position}.` **{item.Title}** - {item.Author ?? UnknownAuthor} `[{duration}]`";
    }

    public static MessageComponent BuildQueuePageControls(int page, int totalPages)
    {
        return new ComponentBuilder()
            .WithButton("Prev", $"queue:page:{page - 1}", ButtonStyle.Secondary,
                new Emoji("\u25C0\uFE0F"), disabled: page <= 0)
            .WithButton("Next", $"queue:page:{page + 1}", ButtonStyle.Secondary,
                new Emoji("\u25B6\uFE0F"), disabled: page >= totalPages - 1)
            .Build();
    }
}