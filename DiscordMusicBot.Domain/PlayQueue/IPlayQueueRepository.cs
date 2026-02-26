namespace DiscordMusicBot.Domain.PlayQueue;

public interface IPlayQueueRepository
{
    Task AddItemsAsync(ulong guildId, IReadOnlyList<PlayQueueItem> items, CancellationToken cancellationToken = default);
    Task<PlayQueueItem?> PeekNextAsync(ulong guildId, int skip = 0, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(ulong guildId, long itemId, CancellationToken cancellationToken = default);
    Task<int> DeleteTopNAsync(ulong guildId, int count, long? excludeItemId = null, CancellationToken cancellationToken = default);
    Task ShuffleAsync(ulong guildId, long? excludeItemId = null, CancellationToken cancellationToken = default);
    Task ClearAsync(ulong guildId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayQueueItem>> GetPageAsync(ulong guildId, int skip, int take, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(ulong guildId, CancellationToken cancellationToken = default);
    Task<(int Count, long TotalDurationMs)> GetCountAndTotalDurationMsAsync(ulong guildId, CancellationToken cancellationToken = default);
}
