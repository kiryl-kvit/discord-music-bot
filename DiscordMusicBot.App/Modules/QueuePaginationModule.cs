using Discord.Interactions;
using DiscordMusicBot.App.Services;

namespace DiscordMusicBot.App.Modules;

public sealed class QueuePaginationModule(QueuePlaybackService queuePlaybackService) : InteractionModuleBase
{
    [ComponentInteraction("queue:page:*")]
    public async Task HandlePageAsync(int page)
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

        await ((Discord.IComponentInteraction)Context.Interaction).UpdateAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }
}