using Discord;
using DiscordMusicBot.Core.Formatters;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services.Queue;

public static class QueueEmbedBuilder
{
    public const int PageSize = 10;

    public static Embed BuildQueueEmbed(IReadOnlyCollection<PlayQueueItem> items, PlayQueueItem? currentItem,
        int page, int pageSize, QueueStats stats, bool autoplayEnabled)
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
            builder.AddField("Up Next", "Queue is empty. Use `/queue add <url>` to add tracks.");
        }
        else
        {
            var lines = items.Select((item, index) => FormatQueueLine(item, page, pageSize, index)).ToList();
            var fieldValue = TruncateToFieldLimit(lines);
            builder.AddField("Up Next", fieldValue);
        }

        var totalPages = stats.Count == 0 ? 1 : (int)Math.Ceiling((double)stats.Count / pageSize);
        var durationText = DateFormatter.FormatTime(stats.TotalDuration);
        var autoplayText = DisplayConstants.FormatAutoplayStatus(autoplayEnabled);
        builder.WithFooter($"Page {page}/{totalPages}  |  {stats.Count} tracks  |  {durationText} total  |  {autoplayText}");

        return builder.Build();
    }

    private static string FormatNowPlaying(PlayQueueItem item)
    {
        return $"{item.Title} - {DisplayConstants.AuthorOrDefault(item.Author)} (requested by {DiscordFormatter.MentionUser(item.UserId)})";
    }

    private static string FormatQueueLine(PlayQueueItem item, int page, int pageSize, int index)
    {
        var position = (page - 1) * pageSize + index + 1;
        var duration = DateFormatter.FormatTimeOrDefault(item.Duration);
        return $"`{position}.` **{item.Title}** - {DisplayConstants.AuthorOrDefault(item.Author)} `[{duration}]` - added by {DiscordFormatter.MentionUser(item.UserId)}";
    }

    private static string TruncateToFieldLimit(List<string> lines)
    {
        const int maxLength = 1024;
        const string ellipsis = "\n*...list truncated*";

        var result = string.Join('\n', lines);
        if (result.Length <= maxLength)
        {
            return result;
        }

        var budget = maxLength - ellipsis.Length;
        var length = 0;
        var count = 0;

        foreach (var line in lines)
        {
            var needed = count == 0 ? line.Length : line.Length + 1;
            if (length + needed > budget)
            {
                break;
            }

            length += needed;
            count++;
        }

        return string.Join('\n', lines.Take(count)) + ellipsis;
    }

    public static MessageComponent BuildQueuePageControls(int page, bool hasNextPage)
    {
        return new ComponentBuilder()
            .WithButton("Prev", $"queue:page:{page - 1}", ButtonStyle.Secondary,
                new Emoji("\u25C0\uFE0F"), disabled: page <= 1)
            .WithButton("Next", $"queue:page:{page + 1}", ButtonStyle.Secondary,
                new Emoji("\u25B6\uFE0F"), disabled: !hasNextPage)
            .Build();
    }

    public static Embed BuildAddedToQueueEmbed(PlayQueueItem[] items)
    {
        var builder = new EmbedBuilder()
            .WithColor(Color.Green);

        if (items.Length == 1)
        {
            var item = items[0];
            var duration = DateFormatter.FormatTimeOrDefault(item.Duration);

            builder.WithTitle("Added to Queue");
            builder.WithDescription($"**{item.Title}**\nRequested by {DiscordFormatter.MentionUser(item.UserId)}");
            builder.AddField("Artist", DisplayConstants.AuthorOrDefault(item.Author), inline: true);
            builder.AddField("Duration", duration, inline: true);
        }
        else
        {
            builder.WithTitle($"Added {items.Length} Tracks to Queue");
            builder.WithDescription($"Requested by {DiscordFormatter.MentionUser(items[0].UserId)}");

            var totalDuration = items
                .Where(x => x.Duration.HasValue)
                .Sum(x => x.Duration!.Value.TotalSeconds);

            var formattedTotal = totalDuration > 0
                ? DateFormatter.FormatTime(TimeSpan.FromSeconds(totalDuration))
                : DisplayConstants.UnknownDuration;

            builder.AddField("Total Tracks", items.Length, inline: true);
            builder.AddField("Total Duration", formattedTotal, inline: true);

            var previewItems = items.Take(3).Select(item =>
            {
                var duration = DateFormatter.FormatTimeOrDefault(item.Duration);
                return $"**{item.Title}** - {DisplayConstants.AuthorOrDefault(item.Author)} `[{duration}]`";
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

    public static Embed BuildSkippedEmbed(PlayQueueItem? skippedItem, int totalSkipped, PlayQueueItem? nextItem)
    {
        var builder = new EmbedBuilder()
            .WithColor(Color.Orange);

        if (totalSkipped > 1)
        {
            builder.WithTitle($"Skipped {totalSkipped} tracks");

            if (skippedItem is not null)
            {
                builder.WithDescription(
                    $"Starting from: **{skippedItem.Title}** - {DisplayConstants.AuthorOrDefault(skippedItem.Author)} (requested by {DiscordFormatter.MentionUser(skippedItem.UserId)})");
            }
        }
        else if (skippedItem is not null)
        {
            builder.WithTitle("Skipped")
                .WithDescription($"**{skippedItem.Title}** - {DisplayConstants.AuthorOrDefault(skippedItem.Author)} (requested by {DiscordFormatter.MentionUser(skippedItem.UserId)})");
        }
        else
        {
            builder.WithTitle("Skipped");
        }

        if (nextItem is not null)
        {
            var duration = DateFormatter.FormatTimeOrDefault(nextItem.Duration);

            builder.AddField("Up Next",
                $"**{nextItem.Title}** - {DisplayConstants.AuthorOrDefault(nextItem.Author)} `[{duration}]` - added by {DiscordFormatter.MentionUser(nextItem.UserId)}");
        }
        else
        {
            builder.AddField("Up Next", "Queue is empty. Use `/queue add <url>` to add tracks.");
        }

        return builder.Build();
    }

    public static Embed BuildAutoplayEmbed(string title, string? author)
    {
        return new EmbedBuilder()
            .WithTitle("Autoplay")
            .WithColor(Color.Teal)
            .WithDescription($"**{title}** - {DisplayConstants.AuthorOrDefault(author)}")
            .WithFooter("Playing a related track because the queue is empty")
            .Build();
    }

    public static Embed BuildAutoplayToggledEmbed(bool enabled)
    {
        return new EmbedBuilder()
            .WithTitle("Autoplay")
            .WithColor(enabled ? Color.Green : Color.LightGrey)
            .WithDescription(enabled
                ? "Autoplay is now **enabled**. Related tracks will play when the queue is empty."
                : "Autoplay is now **disabled**.")
            .Build();
    }
}