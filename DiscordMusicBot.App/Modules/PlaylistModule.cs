using Discord.Interactions;
using DiscordMusicBot.App.Modules.Autocomplete;
using DiscordMusicBot.App.Options;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Domain.Playlists;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusicBot.App.Modules;

[Group("playlist", "Playlist commands")]
public sealed class PlaylistModule(
    IPlaylistRepository playlistRepository,
    QueuePlaybackService queuePlaybackService,
    IOptionsMonitor<PlaylistsOptions> playlistsOptions,
    ILogger<PlaylistModule> logger) : InteractionModuleBase
{
    [SlashCommand("save", "Save the current queue as a named playlist")]
    public async Task SaveAsync(string name)
    {
        var userId = Context.User.Id;
        var guildId = Context.Guild.Id;

        logger.LogInformation("User {UserId} is saving queue as playlist {Name} in guild {GuildId}",
            userId, name, guildId);
        await DeferAsync(ephemeral: true);

        if (string.IsNullOrWhiteSpace(name))
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Content = null;
                props.Embed = ErrorEmbedBuilder.Build("Invalid Name", "Playlist name cannot be empty.");
            });
            return;
        }

        var trimmedName = name.Trim();

        var count = await playlistRepository.GetCountAsync(userId);
        var options = playlistsOptions.CurrentValue;

        if (options.IsLimitReached(count))
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Content = null;
                props.Embed = ErrorEmbedBuilder.Build("Playlist Limit Reached",
                    $"You have reached the playlist limit ({options.Limit}).",
                    "Delete some playlists before creating new ones.");
            });
            return;
        }

        if (await playlistRepository.ExistsByNameAsync(userId, trimmedName))
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Content = null;
                props.Embed = ErrorEmbedBuilder.Build("Duplicate Name",
                    $"A playlist named **{trimmedName}** already exists.",
                    "Choose a different name or delete the existing playlist first.");
            });
            return;
        }

        var currentItem = queuePlaybackService.GetCurrentItem(guildId);
        var itemLimit = options.ItemLimit > 0 ? options.ItemLimit : int.MaxValue;
        var queueTake = currentItem is not null ? itemLimit - 1 : itemLimit;
        var queueItems = await queuePlaybackService.GetQueueItemsAsync(guildId, skip: 0, take: queueTake);

        var totalTracks = queueItems.Count + (currentItem is not null ? 1 : 0);

        if (totalTracks == 0)
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Content = null;
                props.Embed = ErrorEmbedBuilder.Build("Empty Queue",
                    "There are no tracks in the queue to save.",
                    "Use `/queue add <url>` to add tracks first.");
            });
            return;
        }

        var playlistItems = new List<PlaylistItem>(totalTracks);
        var position = 0;
        long? totalDurationMs = 0;

        if (currentItem is not null)
        {
            var durationMs = currentItem.Duration.HasValue
                ? (long?)currentItem.Duration.Value.TotalMilliseconds
                : null;

            playlistItems.Add(PlaylistItem.Create(
                position++, currentItem.Url, currentItem.Title,
                currentItem.Author, durationMs, currentItem.ThumbnailUrl));

            totalDurationMs = AccumulateDuration(totalDurationMs, durationMs);
        }

        foreach (var queueItem in queueItems)
        {
            var durationMs = queueItem.Duration.HasValue
                ? (long?)queueItem.Duration.Value.TotalMilliseconds
                : null;

            playlistItems.Add(PlaylistItem.Create(
                position++, queueItem.Url, queueItem.Title,
                queueItem.Author, durationMs, queueItem.ThumbnailUrl));

            totalDurationMs = AccumulateDuration(totalDurationMs, durationMs);
        }

        var playlist = Playlist.Create(userId, trimmedName, playlistItems.Count, totalDurationMs);
        await playlistRepository.CreateAsync(playlist, playlistItems);

        logger.LogInformation("User {UserId} saved playlist {Name} with {TrackCount} tracks",
            userId, trimmedName, playlistItems.Count);

        var embed = PlaylistEmbedBuilder.BuildSavedEmbed(playlist);
        await ModifyOriginalResponseAsync(props =>
        {
            props.Content = null;
            props.Embed = embed;
        });
    }

    [SlashCommand("list", "Show your playlists")]
    public async Task ListAsync([MinValue(1)] int page = 1)
    {
        var userId = Context.User.Id;

        page = Math.Max(1, page);
        const int pageSize = PlaylistEmbedBuilder.PageSize;
        var skip = (page - 1) * pageSize;

        var items = await playlistRepository.GetByUserAsync(userId, skip, pageSize);
        var totalCount = await playlistRepository.GetCountAsync(userId);

        var embed = PlaylistEmbedBuilder.BuildListEmbed(items, page, pageSize, totalCount);
        var components = PlaylistEmbedBuilder.BuildListPageControls(page,
            hasNextPage: skip + items.Count < totalCount);

        await RespondAsync(embed: embed, components: components, ephemeral: true);
    }

    [SlashCommand("view", "View tracks in a playlist")]
    public async Task ViewAsync(
        [Summary("playlist"), Autocomplete(typeof(PlaylistAutocompleteHandler))] string playlistIdStr,
        [MinValue(1)] int page = 1)
    {
        var userId = Context.User.Id;

        if (!long.TryParse(playlistIdStr, out var playlistId))
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Invalid Selection", "The playlist selection is invalid."),
                ephemeral: true);
            return;
        }

        var playlist = await playlistRepository.GetByIdAsync(playlistId);
        if (playlist is null || playlist.UserId != userId)
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Not Found", "Playlist not found."),
                ephemeral: true);
            return;
        }

        page = Math.Max(1, page);
        const int pageSize = PlaylistEmbedBuilder.PageSize;
        var skip = (page - 1) * pageSize;

        var items = await playlistRepository.GetItemsAsync(playlistId, skip, pageSize);

        var embed = PlaylistEmbedBuilder.BuildViewEmbed(playlist, items, page, pageSize, playlist.TrackCount);
        var components = PlaylistEmbedBuilder.BuildViewPageControls(playlistId, page,
            hasNextPage: skip + items.Count < playlist.TrackCount);

        await RespondAsync(embed: embed, components: components, ephemeral: true);
    }

    [SlashCommand("delete", "Delete a playlist")]
    public async Task DeleteAsync(
        [Summary("playlist"), Autocomplete(typeof(PlaylistAutocompleteHandler))] string playlistIdStr)
    {
        var userId = Context.User.Id;

        if (!long.TryParse(playlistIdStr, out var playlistId))
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Invalid Selection", "The playlist selection is invalid."),
                ephemeral: true);
            return;
        }

        var playlist = await playlistRepository.GetByIdAsync(playlistId);
        if (playlist is null || playlist.UserId != userId)
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Not Found", "Playlist not found."),
                ephemeral: true);
            return;
        }

        await playlistRepository.DeleteAsync(playlistId, userId);

        logger.LogInformation("User {UserId} deleted playlist {PlaylistId}: {Name}",
            userId, playlistId, playlist.Name);

        var embed = PlaylistEmbedBuilder.BuildDeletedEmbed(playlist);
        await RespondAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("rename", "Rename a playlist")]
    public async Task RenameAsync(
        [Summary("playlist"), Autocomplete(typeof(PlaylistAutocompleteHandler))] string playlistIdStr,
        [Summary("new_name")] string newName)
    {
        var userId = Context.User.Id;

        if (!long.TryParse(playlistIdStr, out var playlistId))
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Invalid Selection", "The playlist selection is invalid."),
                ephemeral: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Invalid Name", "New name cannot be empty."),
                ephemeral: true);
            return;
        }

        var playlist = await playlistRepository.GetByIdAsync(playlistId);
        if (playlist is null || playlist.UserId != userId)
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Not Found", "Playlist not found."),
                ephemeral: true);
            return;
        }

        var trimmedNewName = newName.Trim();

        if (await playlistRepository.ExistsByNameAsync(userId, trimmedNewName))
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Duplicate Name",
                    $"A playlist named **{trimmedNewName}** already exists.",
                    "Choose a different name."),
                ephemeral: true);
            return;
        }

        var oldName = playlist.Name;
        playlist.Rename(trimmedNewName);

        await playlistRepository.RenameAsync(playlistId, userId, playlist.Name);

        logger.LogInformation("User {UserId} renamed playlist {PlaylistId}: {OldName} -> {NewName}",
            userId, playlistId, oldName, playlist.Name);

        var embed = PlaylistEmbedBuilder.BuildRenamedEmbed(playlist, oldName);
        await RespondAsync(embed: embed, ephemeral: true);
    }

    private static long? AccumulateDuration(long? total, long? itemDurationMs)
    {
        if (!itemDurationMs.HasValue)
        {
            return null;
        }

        if (!total.HasValue)
        {
            return null;
        }

        return total.Value + itemDurationMs.Value;
    }
}
