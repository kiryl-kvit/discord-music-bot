using Discord.Interactions;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Domain.Playlists;

namespace DiscordMusicBot.App.Modules;

public sealed class PlaylistPaginationModule(IPlaylistRepository playlistRepository) : InteractionModuleBase
{
    [ComponentInteraction("playlist:list:page:*")]
    public async Task HandleListPageAsync(int page)
    {
        var userId = Context.User.Id;

        page = Math.Max(1, page);
        const int pageSize = PlaylistEmbedBuilder.PageSize;
        var skip = (page - 1) * pageSize;

        var items = await playlistRepository.GetByUserAsync(userId, skip, pageSize);
        var totalCount = await playlistRepository.GetCountAsync(userId);

        var embed = PlaylistEmbedBuilder.BuildListEmbed(items, page, pageSize, totalCount);
        var components = PlaylistEmbedBuilder.BuildListPageControls(page,
            hasNextPage: skip + items.Count < totalCount);

        await ((Discord.IComponentInteraction)Context.Interaction).UpdateAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }

    [ComponentInteraction("playlist:view:*:page:*")]
    public async Task HandleViewPageAsync(long playlistId, int page)
    {
        var userId = Context.User.Id;

        var playlist = await playlistRepository.GetByIdAsync(playlistId);
        if (playlist is null || playlist.UserId != userId)
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Not Found", "Playlist not found."),
                ephemeral: true);
            return;
        }

        page = Math.Max(1, page);
        const int pageSize = PlaylistEmbedBuilder.PageSize;
        var skip = (page - 1) * pageSize;

        var items = await playlistRepository.GetItemsAsync(playlistId, skip, pageSize);

        var embed = PlaylistEmbedBuilder.BuildViewEmbed(playlist, items, page, pageSize, playlist.TrackCount);
        var components = PlaylistEmbedBuilder.BuildViewPageControls(playlistId, page,
            hasNextPage: skip + items.Count < playlist.TrackCount);

        await ((Discord.IComponentInteraction)Context.Interaction).UpdateAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }
}
