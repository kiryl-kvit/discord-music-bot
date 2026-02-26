using Discord.Interactions;
using DiscordMusicBot.App.Modules.Autocomplete;
using DiscordMusicBot.App.Options;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using DiscordMusicBot.Domain.Favorites;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusicBot.App.Modules;

[Group("fav", "Favorites commands")]
public sealed class FavoriteModule(
    IFavoriteRepository favoriteRepository,
    IUrlProcessorFactory urlProcessorFactory,
    IOptionsMonitor<FavoritesOptions> favoritesOptions,
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
            await ModifyOriginalResponseAsync(props => props.Content =
                $"Unsupported source. {SupportedSources.GetSupportedSourcesMessage()}");
            return;
        }

        var count = await favoriteRepository.GetCountAsync(userId);

        if (favoritesOptions.CurrentValue.IsLimitReached(count))
        {
            await ModifyOriginalResponseAsync(props => props.Content =
                $"You have reached the favorites limit ({favoritesOptions.CurrentValue.Limit}). Remove some favorites before adding new ones.");
            return;
        }

        var normalizedUrl = UrlNormalizer.TryNormalize(url);
        if (normalizedUrl is null)
        {
            await ModifyOriginalResponseAsync(props => props.Content = "Could not process this URL.");
            return;
        }

        if (await favoriteRepository.ExistsByUrlAsync(userId, normalizedUrl))
        {
            await ModifyOriginalResponseAsync(props => props.Content = "This item is already in your favorites.");
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

        var musicResult = musicItemsResult.Value!;
        var musicItems = musicResult.Items;
        var isPlaylist = musicItems.Count > 1;
        var representative = musicItems.First();
        var storedUrl = isPlaylist ? normalizedUrl : representative.Url;
        var title = isPlaylist ? (musicResult.PlaylistName ?? representative.Title) : representative.Title;

        var favoriteItem = FavoriteItem.Create(
            userId, storedUrl, title, alias, representative.Author,
            isPlaylist ? null : representative.Duration, isPlaylist);

        await favoriteRepository.AddAsync(favoriteItem);

        logger.LogInformation("User {UserId} added favorite: {Title} (playlist={IsPlaylist})",
            userId, title, isPlaylist);

        var embed = FavoriteEmbedBuilder.BuildAddedEmbed(favoriteItem);
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
            await RespondAsync("Invalid favorite selection.", ephemeral: true);
            return;
        }

        var item = await favoriteRepository.GetByIdAsync(favoriteId);
        if (item is null || item.UserId != userId)
        {
            await RespondAsync("Favorite not found.", ephemeral: true);
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
            await RespondAsync("Invalid favorite selection.", ephemeral: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            await RespondAsync("New name cannot be empty.", ephemeral: true);
            return;
        }

        var item = await favoriteRepository.GetByIdAsync(favoriteId);
        if (item is null || item.UserId != userId)
        {
            await RespondAsync("Favorite not found.", ephemeral: true);
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
