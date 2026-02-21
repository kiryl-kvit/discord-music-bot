using Discord.Interactions;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Modules;

[Group("queue", "Queue commands")]
public class QueueModule(
    IUrlProcessorFactory urlProcessorFactory,
    QueuePlaybackService queuePlaybackService,
    ILogger<QueueModule> logger) : InteractionModuleBase
{
    [SlashCommand("add", "Enqueue an item")]
    public async Task AddAsync(string url)
    {
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;

        logger.LogInformation("User {UserId} is trying to enqueue {Url} in guild {GuildId}", userId, url, guildId);
        await DeferAsync(ephemeral: true);

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

        await queuePlaybackService.EnqueueItemsAsync(guildId, queueItems);

        logger.LogInformation("User {UserId} enqueued {Url} in guild {GuildId}", userId, url, guildId);

        var embed = QueueEmbedBuilder.BuildAddedToQueueEmbed(queueItems);
        await ModifyOriginalResponseAsync(props =>
        {
            props.Content = null;
            props.Embed = embed;
        });
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

        await queuePlaybackService.StartAsync(guildId);

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

    [SlashCommand("skip", "Skip the current track")]
    public async Task SkipAsync()
    {
        var guildId = Context.Guild.Id;

        if (!queuePlaybackService.IsPlaying(guildId))
        {
            await RespondAsync("Queue is not playing.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var (skipped, next) = await queuePlaybackService.SkipAsync(guildId);
        
        var embed = QueueEmbedBuilder.BuildSkippedEmbed(skipped, next);
        await ModifyOriginalResponseAsync(props => props.Embed = embed);
    }

    [SlashCommand("list", "Show the queue")]
    public async Task ListAsync(int page = 1)
    {
        var guildId = Context.Guild.Id;

        const int pageSize = QueueEmbedBuilder.PageSize;
        var pageIndex = page - 1;
        var skip = pageIndex * pageSize;

        var items = queuePlaybackService.GetQueueItems(guildId, skip, take: pageSize);
        var currentItem = queuePlaybackService.GetCurrentItem(guildId);

        var embed = QueueEmbedBuilder.BuildQueueEmbed(items, currentItem, page, pageSize);
        var components = QueueEmbedBuilder.BuildQueuePageControls(page, hasNextPage: items.Count == pageSize);

        await RespondAsync(embed: embed, components: components);
    }
}