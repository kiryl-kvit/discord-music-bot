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

        var items = await queuePlaybackService.GetQueueItemsAsync(guildId, skip, take: pageSize);
        var currentItem = queuePlaybackService.GetCurrentItem(guildId);

        var embed = QueueEmbedBuilder.BuildQueueEmbed(items, currentItem, page, pageSize);
        var components = QueueEmbedBuilder.BuildQueuePageControls(page, hasNextPage: items.Count == pageSize);

        await ((Discord.IComponentInteraction)Context.Interaction).UpdateAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }
}