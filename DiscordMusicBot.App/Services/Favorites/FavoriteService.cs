using DiscordMusicBot.App.Options;
using DiscordMusicBot.Core;
using DiscordMusicBot.Domain.Favorites;
using Microsoft.Extensions.Options;

namespace DiscordMusicBot.App.Services.Favorites;

public sealed class FavoriteService(
    IFavoriteRepository favoriteRepository,
    IOptionsMonitor<FavoritesOptions> favoritesOptions)
{
    public async Task<Result<FavoriteItem>> AddAsync(
        ulong userId, string url, string title, string? alias,
        string? author, TimeSpan? duration, bool isPlaylist, string? thumbnailUrl)
    {
        var count = await favoriteRepository.GetCountAsync(userId);

        if (favoritesOptions.CurrentValue.IsLimitReached(count))
        {
            return Result<FavoriteItem>.Failure(
                $"You have reached the favorites limit ({favoritesOptions.CurrentValue.Limit}). " +
                "Remove some favorites before adding new ones.");
        }

        if (await favoriteRepository.ExistsByUrlAsync(userId, url))
        {
            return Result<FavoriteItem>.Failure("This item is already in your favorites.");
        }

        var item = FavoriteItem.Create(
            userId, url, title, alias, author, duration, isPlaylist, thumbnailUrl);

        await favoriteRepository.AddAsync(item);

        return Result<FavoriteItem>.Success(item);
    }
}
