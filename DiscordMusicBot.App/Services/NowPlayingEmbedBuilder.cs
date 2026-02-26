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
}
