using Discord;
using DiscordMusicBot.Core.Formatters;
using DiscordMusicBot.Domain.PlayQueue;
using DiscordMusicBot.Domain.PlayQueue.Dto;

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

    public static Embed BuildAddedToQueueEmbed(EnqueueItemDto[] items)
    {
        var builder = new EmbedBuilder()
            .WithColor(Color.Green);

        if (items.Length == 1)
        {
            var item = items[0];
            var duration = item.Duration is not null
                ? DateFormatter.FormatTime(item.Duration.Value)
                : UnknownDuration;

            builder.WithTitle("Added to Queue");
            builder.WithDescription($"**{item.Title}**");
            builder.AddField("Artist", item.Author ?? UnknownAuthor, inline: true);
            builder.AddField("Duration", duration, inline: true);
        }
        else
        {
            builder.WithTitle($"Added {items.Length} Tracks to Queue");

            var totalDuration = items
                .Where(x => x.Duration.HasValue)
                .Sum(x => x.Duration!.Value.TotalSeconds);

            var formattedTotal = totalDuration > 0
                ? DateFormatter.FormatTime(TimeSpan.FromSeconds(totalDuration))
                : UnknownDuration;

            builder.AddField("Total Tracks", items.Length, inline: true);
            builder.AddField("Total Duration", formattedTotal, inline: true);

            var previewItems = items.Take(3).Select(item =>
            {
                var duration = item.Duration is not null
                    ? DateFormatter.FormatTime(item.Duration.Value)
                    : UnknownDuration;
                return $"**{item.Title}** - {item.Author ?? UnknownAuthor} `[{duration}]`";
            });

            var preview = string.Join('\n', previewItems);
            if (items.Length > 3)
            {
                preview += $"\n*...and {items.Length - 3} more*";
            }

            builder.AddField("Preview", preview);
        }

        return builder.Build();
    }

    public static Embed BuildNowPlayingEmbed(PlayQueueItem item)
    {
        var duration = item.Duration is not null
            ? DateFormatter.FormatTime(item.Duration.Value)
            : UnknownDuration;

        var builder = new EmbedBuilder()
            .WithTitle("Now Playing")
            .WithDescription($"**{item.Title}**")
            .WithColor(Color.Blue)
            .AddField("Artist", item.Author ?? UnknownAuthor, inline: true)
            .AddField("Duration", duration, inline: true);

        return builder.Build();
    }

    public static Embed BuildSkippedEmbed(PlayQueueItem? skippedItem, PlayQueueItem? nextItem)
    {
        var builder = new EmbedBuilder()
            .WithColor(Color.Orange);

        if (skippedItem is not null)
        {
            builder.WithTitle("Skipped")
                .WithDescription($"**{skippedItem.Title}** - {skippedItem.Author ?? UnknownAuthor}");
        }
        else
        {
            builder.WithTitle("Skipped");
        }

        if (nextItem is not null)
        {
            var duration = nextItem.Duration is not null
                ? DateFormatter.FormatTime(nextItem.Duration.Value)
                : UnknownDuration;

            builder.AddField("Up Next",
                $"**{nextItem.Title}** - {nextItem.Author ?? UnknownAuthor} `[{duration}]`");
        }
        else
        {
            builder.AddField("Up Next", "Queue is empty");
        }

        return builder.Build();
    }
}