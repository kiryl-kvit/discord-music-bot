using Discord.Interactions;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Domain.History;

namespace DiscordMusicBot.App.Modules;

public sealed class HistoryPaginationModule(IHistoryRepository historyRepository) : InteractionModuleBase
{
    [ComponentInteraction("history:page:*")]
    public async Task HandlePageAsync(int page)
    {
        var guildId = Context.Guild.Id;

        page = Math.Max(1, page);
        const int pageSize = HistoryEmbedBuilder.PageSize;
        var skip = (page - 1) * pageSize;

        var items = await historyRepository.GetPageAsync(guildId, skip, pageSize);
        var totalCount = await historyRepository.GetCountAsync(guildId);

        var embed = HistoryEmbedBuilder.BuildListEmbed(items, page, pageSize, totalCount);
        var components = HistoryEmbedBuilder.BuildPageControls(page, hasNextPage: skip + items.Count < totalCount);

        await ((Discord.IComponentInteraction)Context.Interaction).UpdateAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }
}
