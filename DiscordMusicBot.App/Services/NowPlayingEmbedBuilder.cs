using Discord;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services;

public static class NowPlayingEmbedBuilder
{
    private const int ProgressBarWidth = 20;

    public static Embed BuildNowPlayingEmbed(PlayQueueItem item, TimeSpan? elapsed, int queueRemaining)
    {
        var duration = item.Duration ?? TimeSpan.Zero;
        var elapsedTime = elapsed ?? TimeSpan.Zero;

        if (elapsedTime > duration && duration > TimeSpan.Zero)
        {
            elapsedTime = duration;
        }

        var progress = duration > TimeSpan.Zero
            ? Math.Clamp(elapsedTime.TotalSeconds / duration.TotalSeconds, 0, 1)
            : 0;

        var progressBar = BuildProgressBar(progress);
        var timeDisplay = $"{FormatTime(elapsedTime)} / {FormatTime(duration)}";

        var builder = new EmbedBuilder()
            .WithTitle("Now Playing")
            .WithDescription($"**{item.Title}**\nby {item.Author ?? "Unknown"}")
            .WithColor(Color.Blue)
            .AddField("Progress", $"{progressBar}\n{timeDisplay}", inline: false);

        if (queueRemaining > 0)
        {
            builder.AddField("Queue", $"{queueRemaining} track{(queueRemaining == 1 ? "" : "s")} remaining",
                inline: true);
        }
        else
        {
            builder.AddField("Queue", "Empty", inline: true);
        }

        return builder.Build();
    }

    public static Embed BuildStoppedEmbed()
    {
        return new EmbedBuilder()
            .WithTitle("Playback Stopped")
            .WithDescription("Nothing is currently playing.")
            .WithColor(Color.LightGrey)
            .Build();
    }

    public static MessageComponent BuildPlayerControls(bool isPlaying)
    {
        return new ComponentBuilder()
            .WithButton("Skip", "player:skip", ButtonStyle.Primary, new Emoji("\u23ED\uFE0F"))
            .WithButton(
                isPlaying ? "Stop" : "Start",
                isPlaying ? "player:stop" : "player:start",
                isPlaying ? ButtonStyle.Danger : ButtonStyle.Success,
                isPlaying ? new Emoji("\u23F9\uFE0F") : new Emoji("\u25B6\uFE0F"))
            .Build();
    }

    public static Embed BuildQueueEmbed(IReadOnlyList<PlayQueueItem> items, PlayQueueItem? currentItem,
        int page, int totalPages)
    {
        var builder = new EmbedBuilder()
            .WithTitle("Queue")
            .WithColor(Color.Teal);

        if (currentItem is not null)
        {
            builder.WithDescription(
                $"**Now playing:** {currentItem.Title} - {currentItem.Author ?? "Unknown"}");
        }

        if (items.Count == 0)
        {
            builder.AddField("Up Next", "Queue is empty.");
        }
        else
        {
            var lines = items.Select((item, index) =>
            {
                var position = (page * PageSize) + index + 1;
                var duration = item.Duration is not null ? FormatTime(item.Duration.Value) : "??:??";
                return $"`{position}.` **{item.Title}** - {item.Author ?? "Unknown"} `[{duration}]`";
            });

            builder.AddField("Up Next", string.Join('\n', lines));
        }

        if (totalPages > 1)
        {
            builder.WithFooter($"Page {page + 1} of {totalPages}");
        }

        return builder.Build();
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

    public const int PageSize = 10;

    private static string BuildProgressBar(double progress)
    {
        var filled = (int)(progress * ProgressBarWidth);
        var empty = ProgressBarWidth - filled;
        return new string('\u2588', filled) + new string('\u2591', empty);
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"mm\:ss");
    }
}
