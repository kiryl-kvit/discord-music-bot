using Discord.Interactions;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Modules;

public class QueuePaginationModule(
    IPlayQueueRepository playQueueRepository,
    QueuePlaybackService playbackService) : InteractionModuleBase
{
    [ComponentInteraction("queue:page:*")]
    public async Task HandlePageAsync(int page)
    {
        var guildId = Context.Guild.Id;

        var items = await playQueueRepository.GetAllAsync(guildId);
        var totalPages = QueueEmbedBuilder.CalculateTotalPages(items.Count);

        var pageIndex = Math.Clamp(page, 0, totalPages - 1);

        var pageItems = items
            .Skip(pageIndex * QueueEmbedBuilder.PageSize)
            .Take(QueueEmbedBuilder.PageSize)
            .ToList();

        var currentItem = playbackService.GetCurrentItem(guildId);
        var embed = QueueEmbedBuilder.BuildQueueEmbed(pageItems, currentItem, pageIndex, totalPages);
        var components = QueueEmbedBuilder.BuildQueuePageControls(pageIndex, totalPages);

        await ((Discord.IComponentInteraction)Context.Interaction).UpdateAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }
}
