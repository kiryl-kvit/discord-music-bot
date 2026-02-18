using Discord.Interactions;
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
    ILogger<QueueModule> logger) : InteractionModuleBase
{
    [SlashCommand("add", "Enqueue an item")]
    public Task AddAsync(string url)
    {
        return AddAsyncCore(url);
    }

    [SlashCommand("a", "Enqueue an item")]
    public Task Alias_A_AddAsync(string url)
    {
        return AddAsyncCore(url);
    }

    private async Task AddAsyncCore(string url)
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
}
