using Discord.Interactions;
using DiscordMusicBot.App.Modules.Autocomplete;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Domain.History;
using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Modules;

[Group("history", "History commands")]
public sealed class HistoryModule(
    IHistoryRepository historyRepository,
    QueuePlaybackService queuePlaybackService,
    ILogger<HistoryModule> logger) : InteractionModuleBase
{
    [SlashCommand("list", "Show recently played tracks")]
    public async Task ListAsync([MinValue(1)] int page = 1)
    {
        var guildId = Context.Guild.Id;

        page = Math.Max(1, page);
        const int pageSize = HistoryEmbedBuilder.PageSize;
        var skip = (page - 1) * pageSize;

        var items = await historyRepository.GetPageAsync(guildId, skip, pageSize);
        var totalCount = await historyRepository.GetCountAsync(guildId);

        var embed = HistoryEmbedBuilder.BuildListEmbed(items, page, pageSize, totalCount);
        var components = HistoryEmbedBuilder.BuildPageControls(page, hasNextPage: skip + items.Count < totalCount);

        await RespondAsync(embed: embed, components: components);
    }

    [SlashCommand("play", "Re-add a track from history to the queue")]
    public async Task PlayAsync(
        [Summary("track"), Autocomplete(typeof(HistoryAutocompleteHandler))] string track)
    {
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;

        await DeferAsync(ephemeral: true);

        if (!long.TryParse(track, out var trackId))
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Embed = ErrorEmbedBuilder.Build("Invalid Selection",
                    "Please select a track from the autocomplete suggestions.");
            });
            return;
        }

        var historyItem = await historyRepository.GetByIdAsync(guildId, trackId);

        if (historyItem is null)
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Embed = ErrorEmbedBuilder.Build("Not Found",
                    "This track is no longer in the history.");
            });
            return;
        }

        var queueItem = PlayQueueItem.Create(guildId, userId, historyItem.SourceType, historyItem.Url, historyItem.Title,
            historyItem.Author, historyItem.Duration, historyItem.ThumbnailUrl);

        await queuePlaybackService.EnqueueItemsAsync(guildId, [queueItem], Context.Channel);

        logger.LogInformation("User {UserId} re-added '{Title}' from history in guild {GuildId}",
            userId, historyItem.Title, guildId);

        var embed = QueueEmbedBuilder.BuildAddedToQueueEmbed([queueItem]);
        await ModifyOriginalResponseAsync(props =>
        {
            props.Content = null;
            props.Embed = embed;
        });
    }
}
