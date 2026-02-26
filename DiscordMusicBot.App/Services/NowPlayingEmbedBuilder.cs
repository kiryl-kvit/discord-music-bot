using Discord;
using DiscordMusicBot.Core.Formatters;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services;

public static class NowPlayingEmbedBuilder
{
    public static Embed BuildEmbed(PlayQueueItem item, bool isPaused)
    {
        var duration = item.Duration is not null
            ? DateFormatter.FormatTime(item.Duration.Value)
            : DisplayConstants.UnknownDuration;

        var statusText = isPaused ? "Paused" : "Now Playing";

        var builder = new EmbedBuilder()
            .WithTitle(statusText)
            .WithColor(isPaused ? Color.LightGrey : Color.Blue)
            .WithDescription($"**{item.Title}**")
            .AddField("Artist", item.Author ?? DisplayConstants.UnknownAuthor, inline: true)
            .AddField("Duration", duration, inline: true)
            .AddField("Requested by", DiscordFormatter.MentionUser(item.UserId), inline: true);

        if (item.ThumbnailUrl is not null)
        {
            builder.WithThumbnailUrl(item.ThumbnailUrl);
        }

        return builder.Build();
    }

    public static MessageComponent BuildControls(bool isPaused)
    {
        return new ComponentBuilder()
            .WithButton(isPaused ? "Resume" : "Pause", "np:pauseresume",
                isPaused ? ButtonStyle.Success : ButtonStyle.Secondary,
                new Emoji(isPaused ? "\u25B6\uFE0F" : "\u23F8\uFE0F"))
            .WithButton("Skip", "np:skip", ButtonStyle.Primary, new Emoji("\u23ED\uFE0F"))
            .WithButton("Shuffle", "np:shuffle", ButtonStyle.Secondary, new Emoji("\uD83D\uDD00"))
            .WithButton("Queue", "np:queue", ButtonStyle.Secondary, new Emoji("\uD83D\uDCCB"))
            .Build();
    }
}
