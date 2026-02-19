using Discord;
using Discord.Interactions;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using DiscordMusicBot.Domain.PlayQueue;
using DiscordMusicBot.Domain.PlayQueue.Dto;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Modules;

[Group("queue", "Queue commands")]
public class QueueModule(
    IPlayQueueRepository playQueueRepository,
    IUrlProcessorFactory urlProcessorFactory,
    QueuePlaybackService queuePlaybackService,
    NowPlayingService nowPlayingService,
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

        var dtoArray = musicItemsResult.Value!
            .Select(x => new EnqueueItemDto(guildId, userId, x.Url, x.Title, x.Author, x.Duration)).ToArray();

        var enqueueResult = await playQueueRepository.EnqueueAsync(dtoArray);

        if (!enqueueResult.IsSuccess)
        {
            await ModifyOriginalResponseAsync(props => props.Content =
                $"Failed to enqueue the item: {enqueueResult.ErrorMessage}");
            return;
        }

        logger.LogInformation("User {UserId} successfully enqueued {Url} in guild {GuildId}", userId, url, guildId);
        await ModifyOriginalResponseAsync(props => props.Content =
            $"{(dtoArray.Length == 1 ? "Item" : $"{dtoArray.Length} items")} added to the queue");
    }

    [SlashCommand("start", "Start or resume queue playback")]
    public async Task StartAsync()
    {
        var guildId = Context.Guild.Id;

        if (queuePlaybackService.IsPlaying(guildId))
        {
            await RespondAsync("Queue is already playing.", ephemeral: true);
            return;
        }

        await queuePlaybackService.StartAsync(guildId);

        if (!nowPlayingService.IsActive(guildId))
        {
            await nowPlayingService.ActivateAsync(guildId, Context.Channel);
        }

        await RespondAsync("Queue started.", ephemeral: true);
    }

    [SlashCommand("stop", "Pause queue playback")]
    public async Task StopAsync()
    {
        var guildId = Context.Guild.Id;

        if (!queuePlaybackService.IsPlaying(guildId))
        {
            await RespondAsync("Queue is not playing.", ephemeral: true);
            return;
        }

        await queuePlaybackService.StopAsync(guildId);
        await RespondAsync("Queue paused.", ephemeral: true);
    }

    [SlashCommand("clear", "Clear all items from the queue")]
    public async Task ClearAsync()
    {
        var guildId = Context.Guild.Id;

        if (queuePlaybackService.IsPlaying(guildId))
        {
            await queuePlaybackService.StopAsync(guildId);
        }

        await playQueueRepository.ClearAsync(guildId);

        logger.LogInformation("Queue cleared in guild {GuildId} by user {UserId}", guildId, Context.User.Id);
        await RespondAsync("Queue cleared.", ephemeral: true);
    }

    [SlashCommand("now", "Show the Now Playing panel")]
    public async Task NowAsync()
    {
        var guildId = Context.Guild.Id;

        await DeferAsync(ephemeral: true);

        await nowPlayingService.ActivateAsync(guildId, Context.Channel);

        await ModifyOriginalResponseAsync(props => props.Content = "Now Playing panel activated.");
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

        queuePlaybackService.Skip(guildId);
        await RespondAsync("Skipped.", ephemeral: true);
    }

    [SlashCommand("list", "Show the queue")]
    public async Task ListAsync(int page = 1)
    {
        var guildId = Context.Guild.Id;

        var items = await playQueueRepository.GetAllAsync(guildId);
        var totalPages = Math.Max(1, (int)Math.Ceiling(items.Count / (double)NowPlayingEmbedBuilder.PageSize));

        var pageIndex = Math.Clamp(page - 1, 0, totalPages - 1);

        var pageItems = items
            .Skip(pageIndex * NowPlayingEmbedBuilder.PageSize)
            .Take(NowPlayingEmbedBuilder.PageSize)
            .ToList();

        var currentItem = queuePlaybackService.GetCurrentItem(guildId);
        var embed = NowPlayingEmbedBuilder.BuildQueueEmbed(pageItems, currentItem, pageIndex, totalPages);
        var components = NowPlayingEmbedBuilder.BuildQueuePageControls(pageIndex, totalPages);

        await RespondAsync(embed: embed, components: components);
    }
}
