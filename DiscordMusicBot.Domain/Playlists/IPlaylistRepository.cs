namespace DiscordMusicBot.Domain.Playlists;

public interface IPlaylistRepository
{
    Task<long> CreateAsync(Playlist playlist, IReadOnlyList<PlaylistItem> items,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long playlistId, ulong userId, CancellationToken cancellationToken = default);

    Task<bool> RenameAsync(long playlistId, ulong userId, string newName,
        CancellationToken cancellationToken = default);

    Task<Playlist?> GetByIdAsync(long playlistId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Playlist>> GetByUserAsync(ulong userId, int skip, int take,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(ulong userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlaylistItem>> GetItemsAsync(long playlistId, int skip, int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlaylistItem>> GetAllItemsAsync(long playlistId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Playlist>> SearchAsync(ulong userId, string query, int limit,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(ulong userId, string name, CancellationToken cancellationToken = default);
}