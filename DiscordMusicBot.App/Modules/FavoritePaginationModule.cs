using Discord.Interactions;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Domain.Favorites;

namespace DiscordMusicBot.App.Modules;

public sealed class FavoritePaginationModule(IFavoriteRepository favoriteRepository) : InteractionModuleBase
{
    [ComponentInteraction("fav:page:*")]
    public async Task HandlePageAsync(int page)
    {
        var userId = Context.User.Id;

        page = Math.Max(1, page);
        const int pageSize = FavoriteEmbedBuilder.PageSize;
        var skip = (page - 1) * pageSize;

        var items = await favoriteRepository.GetByUserAsync(userId, skip, pageSize);
        var totalCount = await favoriteRepository.GetCountAsync(userId);

        var embed = FavoriteEmbedBuilder.BuildListEmbed(items, page, pageSize, totalCount);
        var components = FavoriteEmbedBuilder.BuildPageControls(page, hasNextPage: skip + items.Count < totalCount);

        await ((Discord.IComponentInteraction)Context.Interaction).UpdateAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }
}
