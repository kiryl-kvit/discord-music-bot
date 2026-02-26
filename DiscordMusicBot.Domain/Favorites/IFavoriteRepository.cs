namespace DiscordMusicBot.Domain.Favorites;

public interface IFavoriteRepository
{
    Task<long> AddAsync(FavoriteItem item, CancellationToken cancellationToken = default);
    Task<bool> RemoveByIdAsync(long id, ulong userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUrlAsync(ulong userId, string url, CancellationToken cancellationToken = default);
    Task<FavoriteItem?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FavoriteItem>> GetByUserAsync(ulong userId, int skip, int take, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FavoriteItem>> SearchAsync(ulong userId, string query, int limit, CancellationToken cancellationToken = default);
}
