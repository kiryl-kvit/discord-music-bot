using Discord;
using DiscordMusicBot.Core.Formatters;

namespace DiscordMusicBot.App.Services.NowPlaying;

public static class NowPlayingEmbedBuilder
{
    private const int ProgressBarLength = 16;
    private const char BarFilled = '\u2501'; // ━
    private const char BarEmpty = '\u2500'; // ─
    private const char BarIndicator = '\u25CF'; // ●

    public static Embed BuildEmbed(NowPlayingInfo info)
    {
        var statusText = info.IsPaused ? "Paused" : "Now Playing";
        var color = info.IsPaused ? Color.LightGrey : Color.Blue;

        var description = BuildDescription(info);

        var builder = new EmbedBuilder()
            .WithTitle(statusText)
            .WithColor(color)
            .WithDescription(description);

        if (info.Item.ThumbnailUrl is not null)
        {
            builder.WithThumbnailUrl(info.Item.ThumbnailUrl);
        }

        var footerParts = new List<string>();

        if (info.QueueCount > 0)
        {
            var queueDuration = info.QueueTotalDuration is not null
                ? $" ({DateFormatter.FormatTime(info.QueueTotalDuration.Value)})"
                : "";
            footerParts.Add($"{info.QueueCount} in queue{queueDuration}");
        }
        else
        {
            footerParts.Add("Queue is empty");
        }

        var autoplayText = DisplayConstants.FormatAutoplayStatus(info.IsAutoplayEnabled);
        footerParts.Add(autoplayText);

        builder.WithFooter(string.Join("  |  ", footerParts));

        return builder.Build();
    }

    public static Embed BuildLoadingEmbed()
    {
        return new EmbedBuilder()
            .WithTitle("Loading")
            .WithColor(Color.LightGrey)
            .WithDescription("Loading next track...")
            .Build();
    }

    public static Embed BuildStoppedEmbed()
    {
        return new EmbedBuilder()
            .WithTitle("Playback Ended")
            .WithColor(Color.LightGrey)
            .WithDescription("No more tracks in the queue.")
            .Build();
    }

    public static MessageComponent BuildComponents(bool isPaused)
    {
        var pauseResumeLabel = isPaused ? "Resume" : "Pause";
        var pauseResumeEmoji = isPaused ? new Emoji("\u25B6\uFE0F") : new Emoji("\u23F8\uFE0F");
        var pauseResumeStyle = isPaused ? ButtonStyle.Success : ButtonStyle.Secondary;

        return new ComponentBuilder()
            .WithButton(pauseResumeLabel, "np:pause_resume", pauseResumeStyle, pauseResumeEmoji)
            .WithButton("Skip", "np:skip", ButtonStyle.Secondary, new Emoji("\u23ED\uFE0F"))
            .WithButton("Shuffle", "np:shuffle", ButtonStyle.Secondary, new Emoji("\uD83D\uDD00"))
            .Build();
    }

    public static MessageComponent BuildDisabledComponents()
    {
        return new ComponentBuilder()
            .WithButton("Pause", "np:pause_resume", ButtonStyle.Secondary, new Emoji("\u23F8\uFE0F"),
                disabled: true)
            .WithButton("Skip", "np:skip", ButtonStyle.Secondary, new Emoji("\u23ED\uFE0F"), disabled: true)
            .WithButton("Shuffle", "np:shuffle", ButtonStyle.Secondary, new Emoji("\uD83D\uDD00"), disabled: true)
            .Build();
    }

    private static string BuildDescription(NowPlayingInfo info)
    {
        var item = info.Item;
        var parts = new List<string>
        {
            BuildProgressLine(info.Elapsed, item.Duration, info.IsPaused)
        };

        var titleText = $"**[{item.Title}]({item.Url})**";
        parts.Add(titleText);

        parts.Add(DisplayConstants.AuthorOrDefault(item.Author));

        parts.Add("");

        if (info.NextItem is not null)
        {
            parts.Add(
                $"**Up next:** {info.NextItem.Title} \u2013 {DisplayConstants.AuthorOrDefault(info.NextItem.Author)}");
        }

        parts.Add($"Requested by {DiscordFormatter.MentionUser(item.UserId)}");

        return string.Join('\n', parts);
    }

    private static string BuildProgressLine(TimeSpan elapsed, TimeSpan? totalDuration, bool isPaused)
    {
        var elapsedText = DateFormatter.FormatTime(elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed);
        var totalText = DateFormatter.FormatTimeOrDefault(totalDuration);

        var bar = BuildProgressBar(elapsed, totalDuration);

        var pauseIndicator = isPaused ? " \u23F8" : "";
        return $"{bar} {elapsedText} / {totalText}{pauseIndicator}";
    }

    private static string BuildProgressBar(TimeSpan elapsed, TimeSpan? totalDuration)
    {
        if (totalDuration is null || totalDuration.Value <= TimeSpan.Zero)
        {
            return new string(BarEmpty, ProgressBarLength);
        }

        var progress = Math.Clamp(elapsed / totalDuration.Value, 0.0, 1.0);
        var indicatorPos = (int)(progress * (ProgressBarLength - 1));

        var chars = new char[ProgressBarLength];
        for (var i = 0; i < ProgressBarLength; i++)
        {
            chars[i] = i < indicatorPos ? BarFilled
                : i == indicatorPos ? BarIndicator
                : BarEmpty;
        }

        return new string(chars);
    }
}