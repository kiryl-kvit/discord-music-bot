using Discord;
using DiscordMusicBot.Core.Formatters;
using DiscordMusicBot.Domain.Playlists;

namespace DiscordMusicBot.App.Services;

public static class PlaylistEmbedBuilder
{
    public const int PageSize = 10;

    public static Embed BuildSavedEmbed(Playlist playlist)
    {
        var builder = new EmbedBuilder()
            .WithTitle("Playlist Saved")
            .WithColor(Color.Purple)
            .WithDescription($"**{playlist.Name}**");

        builder.AddField("Tracks", playlist.TrackCount, inline: true);

        if (playlist.TotalDurationMs.HasValue)
        {
            builder.AddField("Duration",
                DateFormatter.FormatTime(TimeSpan.FromMilliseconds(playlist.TotalDurationMs.Value)), inline: true);
        }

        return builder.Build();
    }

    public static Embed BuildListEmbed(IReadOnlyList<Playlist> items, int page, int pageSize, int totalCount)
    {
        var builder = new EmbedBuilder()
            .WithTitle("Playlists")
            .WithColor(Color.Purple)
            .WithFooter($"Page {page} | {totalCount} total playlists");

        if (items.Count == 0)
        {
            builder.WithDescription("No playlists yet. Use `/playlist save <name>` to save the current queue.");
            return builder.Build();
        }

        var lines = items.Select((item, index) => FormatListLine(item, page, pageSize, index));
        builder.WithDescription(string.Join('\n', lines));

        return builder.Build();
    }

    private static string FormatListLine(Playlist playlist, int page, int pageSize, int index)
    {
        var position = (page - 1) * pageSize + index + 1;
        var duration = playlist.TotalDurationMs.HasValue
            ? DateFormatter.FormatTime(TimeSpan.FromMilliseconds(playlist.TotalDurationMs.Value))
            : DisplayConstants.UnknownDuration;
        return $"`{position}.` **{playlist.Name}** - {playlist.TrackCount} tracks `[{duration}]`";
    }

    public static Embed BuildViewEmbed(Playlist playlist, IReadOnlyList<PlaylistItem> items,
        int page, int pageSize, int totalItems)
    {
        var builder = new EmbedBuilder()
            .WithTitle($"Playlist: {playlist.Name}")
            .WithColor(Color.Purple)
            .WithFooter($"Page {page} | {totalItems} total tracks");

        if (items.Count == 0)
        {
            builder.WithDescription("This playlist is empty.");
            return builder.Build();
        }

        var lines = items.Select((item, index) => FormatViewLine(item, page, pageSize, index));
        builder.WithDescription(string.Join('\n', lines));

        return builder.Build();
    }

    private static string FormatViewLine(PlaylistItem item, int page, int pageSize, int index)
    {
        var position = (page - 1) * pageSize + index + 1;
        var duration = item.DurationMs.HasValue
            ? DateFormatter.FormatTime(TimeSpan.FromMilliseconds(item.DurationMs.Value))
            : DisplayConstants.UnknownDuration;
        return $"`{position}.` **{item.Title}** - {item.Author ?? DisplayConstants.UnknownAuthor} `[{duration}]`";
    }

    public static Embed BuildDeletedEmbed(Playlist playlist)
    {
        return new EmbedBuilder()
            .WithTitle("Playlist Deleted")
            .WithColor(Color.Red)
            .WithDescription($"**{playlist.Name}** has been deleted.")
            .Build();
    }

    public static Embed BuildRenamedEmbed(Playlist playlist, string oldName)
    {
        return new EmbedBuilder()
            .WithTitle("Playlist Renamed")
            .WithColor(Color.Purple)
            .WithDescription($"**{oldName}** has been renamed to **{playlist.Name}**.")
            .Build();
    }

    public static MessageComponent BuildListPageControls(int page, bool hasNextPage)
    {
        return new ComponentBuilder()
            .WithButton("Prev", $"playlist:list:page:{page - 1}", ButtonStyle.Secondary,
                new Emoji("\u25C0\uFE0F"), disabled: page <= 1)
            .WithButton("Next", $"playlist:list:page:{page + 1}", ButtonStyle.Secondary,
                new Emoji("\u25B6\uFE0F"), disabled: !hasNextPage)
            .Build();
    }

    public static MessageComponent BuildViewPageControls(long playlistId, int page, bool hasNextPage)
    {
        return new ComponentBuilder()
            .WithButton("Prev", $"playlist:view:{playlistId}:page:{page - 1}", ButtonStyle.Secondary,
                new Emoji("\u25C0\uFE0F"), disabled: page <= 1)
            .WithButton("Next", $"playlist:view:{playlistId}:page:{page + 1}", ButtonStyle.Secondary,
                new Emoji("\u25B6\uFE0F"), disabled: !hasNextPage)
            .Build();
    }
}