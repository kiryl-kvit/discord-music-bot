using Discord.Interactions;
using DiscordMusicBot.App.Modules.Autocomplete;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using DiscordMusicBot.Domain.Favorites;
using DiscordMusicBot.Domain.Playlists;
using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Modules;

[Group("queue", "Queue commands")]
public sealed class QueueModule(
    IUrlProcessorFactory urlProcessorFactory,
    QueuePlaybackService queuePlaybackService,
    IFavoriteRepository favoriteRepository,
    IPlaylistRepository playlistRepository,
    ILogger<QueueModule> logger) : InteractionModuleBase
{
    [SlashCommand("add", "Enqueue an item, a favorite, or a playlist")]
    public async Task AddAsync([Autocomplete(typeof(QueueAddAutocompleteHandler))] string url)
    {
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;

        logger.LogInformation("User {UserId} is trying to enqueue {Url} in guild {GuildId}", userId, url, guildId);
        await DeferAsync(ephemeral: true);

        if (url.StartsWith(FavoriteAutocompleteHelper.FavoritePrefix, StringComparison.Ordinal))
        {
            await EnqueueFromFavoriteAsync(guildId, userId, url[FavoriteAutocompleteHelper.FavoritePrefix.Length..]);
            return;
        }

        if (url.StartsWith(PlaylistAutocompleteHelper.PlaylistPrefix, StringComparison.Ordinal))
        {
            await EnqueueFromPlaylistAsync(guildId, userId, url[PlaylistAutocompleteHelper.PlaylistPrefix.Length..]);
            return;
        }

        var queueItems = await ResolveUrlToQueueItemsAsync(guildId, userId, url);
        if (queueItems is null)
        {
            return;
        }

        await EnqueueAndRespondAsync(guildId, queueItems);
    }

    private async Task EnqueueFromFavoriteAsync(ulong guildId, ulong userId, string favoriteIdStr)
    {
        if (!long.TryParse(favoriteIdStr, out var favoriteId))
        {
            await RespondWithDeferredErrorAsync("Invalid Selection", "The favorite selection is invalid.");
            return;
        }

        var favorite = await favoriteRepository.GetByIdAsync(favoriteId);
        if (favorite is null || favorite.UserId != userId)
        {
            await RespondWithDeferredErrorAsync("Not Found", "Favorite not found.");
            return;
        }

        logger.LogInformation("User {UserId} is enqueueing favorite {FavoriteId} ({Title}) in guild {GuildId}",
            userId, favoriteId, favorite.DisplayName, guildId);

        PlayQueueItem[] queueItems;

        if (favorite.IsPlaylist)
        {
            var resolved = await ResolveUrlToQueueItemsAsync(guildId, userId, favorite.Url);
            if (resolved is null)
            {
                return;
            }

            queueItems = resolved;
        }
        else
        {
            queueItems =
            [
                PlayQueueItem.Create(guildId, userId, favorite.Url, favorite.Title,
                    favorite.Author, favorite.Duration, favorite.ThumbnailUrl)
            ];
        }

        await EnqueueAndRespondAsync(guildId, queueItems);
    }

    private async Task EnqueueFromPlaylistAsync(ulong guildId, ulong userId, string playlistIdStr)
    {
        if (!long.TryParse(playlistIdStr, out var playlistId))
        {
            await RespondWithDeferredErrorAsync("Invalid Selection", "The playlist selection is invalid.");
            return;
        }

        var playlist = await playlistRepository.GetByIdAsync(playlistId);
        if (playlist is null || playlist.UserId != userId)
        {
            await RespondWithDeferredErrorAsync("Not Found", "Playlist not found.");
            return;
        }

        logger.LogInformation("User {UserId} is enqueueing playlist {PlaylistId} ({Name}) in guild {GuildId}",
            userId, playlistId, playlist.Name, guildId);

        var playlistItems = await playlistRepository.GetAllItemsAsync(playlistId);

        if (playlistItems.Count == 0)
        {
            await RespondWithDeferredErrorAsync("Empty Playlist", "This playlist has no tracks.");
            return;
        }

        var queueItems = playlistItems
            .Select(x => PlayQueueItem.Create(guildId, userId, x.Url, x.Title, x.Author,
                x.DurationMs.HasValue ? TimeSpan.FromMilliseconds(x.DurationMs.Value) : null, x.ThumbnailUrl))
            .ToArray();

        await EnqueueAndRespondAsync(guildId, queueItems);
    }

    private async Task<PlayQueueItem[]?> ResolveUrlToQueueItemsAsync(ulong guildId, ulong userId, string url)
    {
        if (!SupportedSources.IsSupported(url))
        {
            await RespondWithDeferredErrorAsync("Unsupported Source",
                SupportedSources.GetSupportedSourcesMessage());
            logger.LogInformation("User {UserId} provided unsupported source {Url} in guild {GuildId}",
                userId, url, guildId);
            return null;
        }

        var urlProcessor = urlProcessorFactory.GetProcessor(url);
        var musicItemsResult = await urlProcessor.GetMusicItemsAsync(url);

        if (!musicItemsResult.IsSuccess)
        {
            await RespondWithDeferredErrorAsync("Failed to Process URL",
                musicItemsResult.ErrorMessage ?? "An unknown error occurred.");
            return null;
        }

        return musicItemsResult.Value!.Items
            .Select(x => PlayQueueItem.Create(guildId, userId, x.Url, x.Title, x.Author, x.Duration, x.ThumbnailUrl))
            .ToArray();
    }

    private async Task EnqueueAndRespondAsync(ulong guildId, PlayQueueItem[] queueItems)
    {
        await queuePlaybackService.EnqueueItemsAsync(guildId, queueItems, Context.Channel);

        var embed = QueueEmbedBuilder.BuildAddedToQueueEmbed(queueItems);
        await ModifyOriginalResponseAsync(props =>
        {
            props.Content = null;
            props.Embed = embed;
        });
    }

    private async Task RespondWithDeferredErrorAsync(string title, string description, string? guidance = null)
    {
        await ModifyOriginalResponseAsync(props =>
        {
            props.Content = null;
            props.Embed = ErrorEmbedBuilder.Build(title, description, guidance);
        });
    }

    [SlashCommand("shuffle", "Shuffle the queue")]
    public async Task ShuffleAsync()
    {
        var guildId = Context.Guild.Id;

        await DeferAsync();

        var result = await queuePlaybackService.ShuffleQueueAsync(guildId);

        if (!result.IsSuccess)
        {
            logger.LogInformation("Failed to shuffle queue in guild {GuildId}: {ErrorMessage}", guildId,
                result.ErrorMessage);
            await ModifyOriginalResponseAsync(props =>
            {
                props.Embed = ErrorEmbedBuilder.Build("Shuffle Failed",
                    result.ErrorMessage ?? "An unknown error occurred.");
            });
            return;
        }

        logger.LogInformation("Queue shuffled in guild {GuildId} by user {UserId}", guildId, Context.User.Id);
        await ModifyOriginalResponseAsync(props =>
            props.Content = $"{Context.User.Mention} shuffled the queue.");
    }

    [SlashCommand("resume", "Resume queue playback")]
    public async Task Resume()
    {
        var guildId = Context.Guild.Id;

        if (queuePlaybackService.IsPlaying(guildId))
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Already Playing", "The queue is already playing."),
                ephemeral: true);
            return;
        }

        await DeferAsync();

        var result = await queuePlaybackService.StartAsync(guildId, Context.Channel);

        if (!result.IsSuccess)
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Embed = ErrorEmbedBuilder.Build("Queue is Empty",
                    "There are no tracks to play.",
                    "Use `/queue add <url>` to add tracks first.");
            });
            return;
        }

        await ModifyOriginalResponseAsync(props =>
            props.Content = $"{Context.User.Mention} resumed the queue.");
    }

    [SlashCommand("pause", "Pause queue playback")]
    public async Task PauseAsync()
    {
        var guildId = Context.Guild.Id;

        if (!queuePlaybackService.IsPlaying(guildId))
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Not Playing", "The queue is not currently playing."),
                ephemeral: true);
            return;
        }

        await DeferAsync();

        await queuePlaybackService.PauseAsync(guildId);
        await ModifyOriginalResponseAsync(props =>
            props.Content = $"{Context.User.Mention} paused the queue.");
    }

    [SlashCommand("clear", "Clear all items from the queue")]
    public async Task ClearAsync()
    {
        var guildId = Context.Guild.Id;

        await DeferAsync();

        await queuePlaybackService.ClearQueueAsync(guildId);

        logger.LogInformation("Queue cleared in guild {GuildId} by user {UserId}", guildId, Context.User.Id);
        await ModifyOriginalResponseAsync(props =>
            props.Content = $"{Context.User.Mention} cleared the queue.");
    }

    [SlashCommand("skip", "Skip one or more tracks")]
    public async Task SkipAsync([MinValue(1)] int count = 1)
    {
        var guildId = Context.Guild.Id;

        if (!queuePlaybackService.IsPlaying(guildId))
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Not Playing", "The queue is not currently playing."),
                ephemeral: true);
            return;
        }

        await DeferAsync();

        var result = await queuePlaybackService.SkipAsync(guildId, count);

        var embed = QueueEmbedBuilder.BuildSkippedEmbed(result.Skipped, result.TotalSkipped, result.Next);
        await ModifyOriginalResponseAsync(props => props.Embed = embed);
    }

    [SlashCommand("list", "Show the queue")]
    public async Task ListAsync([MinValue(1)] int page = 1)
    {
        var guildId = Context.Guild.Id;

        page = Math.Max(1, page);
        const int pageSize = QueueEmbedBuilder.PageSize;
        var pageIndex = page - 1;
        var skip = pageIndex * pageSize;

        var items = await queuePlaybackService.GetQueueItemsAsync(guildId, skip, take: pageSize + 1);
        var currentItem = queuePlaybackService.GetCurrentItem(guildId);
        var stats = await queuePlaybackService.GetQueueStatsAsync(guildId);
        var hasNextPage = items.Count > pageSize;
        var pageItems = hasNextPage ? items.Take(pageSize).ToList() : items;

        var embed = QueueEmbedBuilder.BuildQueueEmbed(pageItems, currentItem, page, pageSize, stats);
        var components = QueueEmbedBuilder.BuildQueuePageControls(page, hasNextPage);

        await RespondAsync(embed: embed, components: components);
    }
}
