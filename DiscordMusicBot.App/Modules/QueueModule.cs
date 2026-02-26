using Discord.Interactions;
using DiscordMusicBot.App.Modules.Autocomplete;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using DiscordMusicBot.Domain.Favorites;
using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Modules;

[Group("queue", "Queue commands")]
public sealed class QueueModule(
    IUrlProcessorFactory urlProcessorFactory,
    QueuePlaybackService queuePlaybackService,
    IFavoriteRepository favoriteRepository,
    ILogger<QueueModule> logger) : InteractionModuleBase
{
    [SlashCommand("add", "Enqueue an item or a favorite")]
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

        if (!SupportedSources.IsSupported(url))
        {
            await ModifyOriginalResponseAsync(props => props.Content =
                $"Unsupported source. {SupportedSources.GetSupportedSourcesMessage()}");
            logger.LogInformation("User {UserId} provided unsupported source {Url} in guild {GuildId}", userId, url,
                guildId);
            return;
        }

        var urlProcessor = urlProcessorFactory.GetProcessor(url);
        var musicItemsResult = await urlProcessor.GetMusicItemsAsync(url);

        if (!musicItemsResult.IsSuccess)
        {
            await ModifyOriginalResponseAsync(props => props.Content =
                $"Failed to process URL: {musicItemsResult.ErrorMessage}");
            return;
        }

        var queueItems = musicItemsResult.Value!
            .Select(x => PlayQueueItem.Create(guildId, userId, x.Url, x.Title, x.Author, x.Duration)).ToArray();

        await queuePlaybackService.EnqueueItemsAsync(guildId, queueItems, Context.Channel);

        logger.LogInformation("User {UserId} enqueued {Url} in guild {GuildId}", userId, url, guildId);

        var embed = QueueEmbedBuilder.BuildAddedToQueueEmbed(queueItems);
        await ModifyOriginalResponseAsync(props =>
        {
            props.Content = null;
            props.Embed = embed;
        });
    }

    private async Task EnqueueFromFavoriteAsync(ulong guildId, ulong userId, string favoriteIdStr)
    {
        if (!long.TryParse(favoriteIdStr, out var favoriteId))
        {
            await ModifyOriginalResponseAsync(props => props.Content = "Invalid favorite selection.");
            return;
        }

        var favorite = await favoriteRepository.GetByIdAsync(favoriteId);
        if (favorite is null || favorite.UserId != userId)
        {
            await ModifyOriginalResponseAsync(props => props.Content = "Favorite not found.");
            return;
        }

        logger.LogInformation("User {UserId} is enqueueing favorite {FavoriteId} ({Title}) in guild {GuildId}",
            userId, favoriteId, favorite.DisplayName, guildId);

        PlayQueueItem[] queueItems;

        if (favorite.IsPlaylist)
        {
            var urlProcessor = urlProcessorFactory.GetProcessor(favorite.Url);
            var musicItemsResult = await urlProcessor.GetMusicItemsAsync(favorite.Url);

            if (!musicItemsResult.IsSuccess)
            {
                await ModifyOriginalResponseAsync(props => props.Content =
                    $"Failed to resolve playlist: {musicItemsResult.ErrorMessage}");
                return;
            }

            queueItems = musicItemsResult.Value!
                .Select(x => PlayQueueItem.Create(guildId, userId, x.Url, x.Title, x.Author, x.Duration)).ToArray();
        }
        else
        {
            queueItems = [PlayQueueItem.Create(guildId, userId, favorite.Url, favorite.Title,
                favorite.Author, favorite.Duration)];
        }

        await queuePlaybackService.EnqueueItemsAsync(guildId, queueItems, Context.Channel);

        logger.LogInformation("User {UserId} enqueued favorite {FavoriteId} in guild {GuildId}",
            userId, favoriteId, guildId);

        var embed = QueueEmbedBuilder.BuildAddedToQueueEmbed(queueItems);
        await ModifyOriginalResponseAsync(props =>
        {
            props.Content = null;
            props.Embed = embed;
        });
    }

    [SlashCommand("shuffle", "Shuffle the queue")]
    public async Task ShuffleAsync()
    {
        var guildId = Context.Guild.Id;

        await DeferAsync(ephemeral: true);

        var result = await queuePlaybackService.ShuffleQueueAsync(guildId);

        if (!result.IsSuccess)
        {
            logger.LogInformation("Failed to shuffle queue in guild {GuildId}: {ErrorMessage}", guildId,
                result.ErrorMessage);
            await ModifyOriginalResponseAsync(props =>
                props.Content = $"Failed to shuffle queue: {result.ErrorMessage}");
            return;
        }

        logger.LogInformation("Queue shuffled in guild {GuildId} by user {UserId}", guildId, Context.User.Id);
        await ModifyOriginalResponseAsync(props => props.Content = "Queue shuffled.");
    }

    [SlashCommand("resume", "Resume queue playback")]
    public async Task Resume()
    {
        var guildId = Context.Guild.Id;

        if (queuePlaybackService.IsPlaying(guildId))
        {
            await RespondAsync("Queue is already playing.", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        await queuePlaybackService.StartAsync(guildId, Context.Channel);

        await ModifyOriginalResponseAsync(props => props.Content = "Queue started.");
    }

    [SlashCommand("pause", "Pause queue playback")]
    public async Task PauseAsync()
    {
        var guildId = Context.Guild.Id;

        if (!queuePlaybackService.IsPlaying(guildId))
        {
            await RespondAsync("Queue is not playing.", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        await queuePlaybackService.PauseAsync(guildId);
        await ModifyOriginalResponseAsync(props => props.Content = "Queue paused.");
    }

    [SlashCommand("clear", "Clear all items from the queue")]
    public async Task ClearAsync()
    {
        var guildId = Context.Guild.Id;

        await DeferAsync(ephemeral: true);

        await queuePlaybackService.ClearQueueAsync(guildId);

        logger.LogInformation("Queue cleared in guild {GuildId} by user {UserId}", guildId, Context.User.Id);
        await ModifyOriginalResponseAsync(props => props.Content = "Queue cleared.");
    }

    [SlashCommand("skip", "Skip one or more tracks")]
    public async Task SkipAsync([MinValue(1)] int count = 1)
    {
        var guildId = Context.Guild.Id;

        if (!queuePlaybackService.IsPlaying(guildId))
        {
            await RespondAsync("Queue is not playing.", ephemeral: true);
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

        var items = await queuePlaybackService.GetQueueItemsAsync(guildId, skip, take: pageSize);
        var currentItem = queuePlaybackService.GetCurrentItem(guildId);

        var embed = QueueEmbedBuilder.BuildQueueEmbed(items, currentItem, page, pageSize);
        var components = QueueEmbedBuilder.BuildQueuePageControls(page, hasNextPage: items.Count == pageSize);

        await RespondAsync(embed: embed, components: components);
    }
}
