namespace DiscordMusicBot.Domain.PlayQueue;

public interface IPlayQueueRepository
{
    Task AddItemsAsync(ulong guildId, IReadOnlyList<PlayQueueItem> items);
    Task<PlayQueueItem?> PeekNextAsync(ulong guildId, int skip = 0);
    Task DeleteByIdAsync(ulong guildId, long itemId);
    Task ShuffleAsync(ulong guildId, long? excludeItemId = null);
    Task ClearAsync(ulong guildId);
    Task<IReadOnlyList<PlayQueueItem>> GetPageAsync(ulong guildId, int skip, int take);
    Task<int> GetCountAsync(ulong guildId);
}
