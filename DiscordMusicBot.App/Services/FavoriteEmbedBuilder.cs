using Discord;
using DiscordMusicBot.Core.Formatters;
using DiscordMusicBot.Domain.Favorites;

namespace DiscordMusicBot.App.Services;

public static class FavoriteEmbedBuilder
{
    public const int PageSize = 10;

    public static Embed BuildAddedEmbed(FavoriteItem item)
    {
        var builder = new EmbedBuilder()
            .WithTitle("Added to Favorites")
            .WithColor(Color.Gold)
            .WithDescription($"**{item.DisplayName}**");

        if (item.IsPlaylist)
        {
            builder.AddField("Type", "Playlist", inline: true);
        }
        else
        {
            builder.AddField("Artist", item.Author ?? DisplayConstants.UnknownAuthor, inline: true);
            builder.AddField("Duration",
                item.Duration is not null ? DateFormatter.FormatTime(item.Duration.Value) : DisplayConstants.UnknownDuration,
                inline: true);
        }

        if (item.Alias is not null)
        {
            builder.AddField("Alias", item.Alias, inline: true);
        }

        return builder.Build();
    }

    public static Embed BuildListEmbed(IReadOnlyList<FavoriteItem> items, int page, int pageSize, int totalCount)
    {
        var builder = new EmbedBuilder()
            .WithTitle("Favorites")
            .WithColor(Color.Gold)
            .WithFooter($"Page {page} | {totalCount} total favorites");

        if (items.Count == 0)
        {
            builder.WithDescription("No favorites yet. Use `/fav add <url>` to save a favorite.");
            return builder.Build();
        }

        var lines = items.Select((item, index) => FormatLine(item, page, pageSize, index));
        builder.WithDescription(string.Join('\n', lines));

        return builder.Build();
    }

    private static string FormatLine(FavoriteItem item, int page, int pageSize, int index)
    {
        var position = (page - 1) * pageSize + index + 1;

        if (item.IsPlaylist)
            return $"`{position}.` **{item.DisplayName}** `[Playlist]`";

        var duration = item.Duration is not null
            ? DateFormatter.FormatTime(item.Duration.Value)
            : DisplayConstants.UnknownDuration;
        return $"`{position}.` **{item.DisplayName}** - {item.Author ?? DisplayConstants.UnknownAuthor} `[{duration}]`";
    }

    public static Embed BuildRemovedEmbed(FavoriteItem item)
    {
        return new EmbedBuilder()
            .WithTitle("Removed from Favorites")
            .WithColor(Color.Red)
            .WithDescription($"**{item.DisplayName}** has been removed from your favorites.")
            .Build();
    }

    public static Embed BuildRenamedEmbed(FavoriteItem item, string oldDisplayName)
    {
        return new EmbedBuilder()
            .WithTitle("Favorite Renamed")
            .WithColor(Color.Gold)
            .WithDescription($"**{oldDisplayName}** has been renamed to **{item.DisplayName}**.")
            .Build();
    }

    public static MessageComponent BuildPageControls(int page, bool hasNextPage)
    {
        return new ComponentBuilder()
            .WithButton("Prev", $"fav:page:{page - 1}", ButtonStyle.Secondary,
                new Emoji("\u25C0\uFE0F"), disabled: page <= 1)
            .WithButton("Next", $"fav:page:{page + 1}", ButtonStyle.Secondary,
                new Emoji("\u25B6\uFE0F"), disabled: !hasNextPage)
            .Build();
    }
}
