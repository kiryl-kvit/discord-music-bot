using Discord.Interactions;
using DiscordMusicBot.App.Services.Common;
using DiscordMusicBot.App.Services.Favorites;
using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using DiscordMusicBot.Domain.Favorites;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Modules.Favorites;

[Group("fav", "Favorites commands")]
public sealed class FavoriteModule(
    IFavoriteRepository favoriteRepository,
    IUrlProcessorFactory urlProcessorFactory,
    FavoriteService favoriteService,
    ILogger<FavoriteModule> logger) : InteractionModuleBase
{
    [SlashCommand("add", "Add a track or playlist to your favorites")]
    public async Task AddAsync(string url, string? alias = null)
    {
        var userId = Context.User.Id;

        logger.LogInformation("User {UserId} is adding {Url} to favorites", userId, url);
        await DeferAsync(ephemeral: true);

        if (!SupportedSources.IsSupported(url))
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Content = null;
                props.Embed = ErrorEmbedBuilder.Build("Unsupported Source",
                    SupportedSources.GetSupportedSourcesMessage());
            });
            return;
        }

        var normalizedUrl = UrlNormalizer.TryNormalize(url);
        if (normalizedUrl is null)
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Content = null;
                props.Embed = ErrorEmbedBuilder.Build("Invalid URL", "Could not process this URL.");
            });
            return;
        }

        var urlProcessor = urlProcessorFactory.GetProcessor(url);
        var musicItemsResult = await urlProcessor.GetMusicItemsAsync(url);

        if (!musicItemsResult.IsSuccess)
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Content = null;
                props.Embed = ErrorEmbedBuilder.Build("Failed to Process URL",
                    musicItemsResult.ErrorMessage ?? "An unknown error occurred.");
            });
            return;
        }

        var musicResult = musicItemsResult.Value!;
        var musicItems = musicResult.Items;
        var isPlaylist = musicItems.Count > 1;
        var representative = musicItems.First();
        var storedUrl = isPlaylist ? normalizedUrl : representative.Url;
        var title = isPlaylist ? (musicResult.PlaylistName ?? representative.Title) : representative.Title;

        var result = await favoriteService.AddAsync(
            userId, storedUrl, title, alias, representative.Author,
            isPlaylist ? null : representative.Duration, isPlaylist, representative.ThumbnailUrl);

        if (!result.IsSuccess)
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Content = null;
                props.Embed = ErrorEmbedBuilder.Build("Cannot Add Favorite", result.ErrorMessage!);
            });
            return;
        }

        logger.LogInformation("User {UserId} added favorite: {Title} (playlist={IsPlaylist})",
            userId, title, isPlaylist);

        var embed = FavoriteEmbedBuilder.BuildAddedEmbed(result.Value!);
        await ModifyOriginalResponseAsync(props =>
        {
            props.Content = null;
            props.Embed = embed;
        });
    }

    [SlashCommand("list", "Show your favorites")]
    public async Task ListAsync([MinValue(1)] int page = 1)
    {
        var userId = Context.User.Id;

        page = Math.Max(1, page);
        const int pageSize = FavoriteEmbedBuilder.PageSize;
        var skip = (page - 1) * pageSize;

        var items = await favoriteRepository.GetByUserAsync(userId, skip, pageSize);
        var totalCount = await favoriteRepository.GetCountAsync(userId);

        var embed = FavoriteEmbedBuilder.BuildListEmbed(items, page, pageSize, totalCount);
        var components = FavoriteEmbedBuilder.BuildPageControls(page, hasNextPage: skip + items.Count < totalCount);

        await RespondAsync(embed: embed, components: components, ephemeral: true);
    }

    [SlashCommand("remove", "Remove a favorite")]
    public async Task RemoveAsync(
        [Summary("favorite"), Autocomplete(typeof(FavoriteAutocompleteHandler))] string favoriteIdStr)
    {
        var userId = Context.User.Id;

        if (!long.TryParse(favoriteIdStr, out var favoriteId))
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Invalid Selection", "The favorite selection is invalid."),
                ephemeral: true);
            return;
        }

        var item = await favoriteRepository.GetByIdAsync(favoriteId);
        if (item is null || item.UserId != userId)
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Not Found", "Favorite not found."),
                ephemeral: true);
            return;
        }

        await favoriteRepository.RemoveByIdAsync(favoriteId, userId);

        logger.LogInformation("User {UserId} removed favorite {FavoriteId}: {Title}", userId, favoriteId, item.Title);

        var embed = FavoriteEmbedBuilder.BuildRemovedEmbed(item);
        await RespondAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("rename", "Rename a favorite")]
    public async Task RenameAsync(
        [Summary("favorite"), Autocomplete(typeof(FavoriteAutocompleteHandler))] string favoriteIdStr,
        [Summary("new_name")] string newName)
    {
        var userId = Context.User.Id;

        if (!long.TryParse(favoriteIdStr, out var favoriteId))
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Invalid Selection", "The favorite selection is invalid."),
                ephemeral: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Invalid Name", "New name cannot be empty."),
                ephemeral: true);
            return;
        }

        var item = await favoriteRepository.GetByIdAsync(favoriteId);
        if (item is null || item.UserId != userId)
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Not Found", "Favorite not found."),
                ephemeral: true);
            return;
        }

        var oldDisplayName = item.DisplayName;
        item.UpdateAlias(newName);

        await favoriteRepository.UpdateAliasAsync(favoriteId, userId, item.Alias!);

        logger.LogInformation("User {UserId} renamed favorite {FavoriteId}: {OldName} -> {NewName}",
            userId, favoriteId, oldDisplayName, item.DisplayName);

        var embed = FavoriteEmbedBuilder.BuildRenamedEmbed(item, oldDisplayName);
        await RespondAsync(embed: embed, ephemeral: true);
    }
}
