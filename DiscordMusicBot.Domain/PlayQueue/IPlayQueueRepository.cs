namespace DiscordMusicBot.Domain.PlayQueue;

public interface IPlayQueueRepository
{
    Task AddItemsAsync(ulong guildId, IReadOnlyList<PlayQueueItem> items);
    Task<PlayQueueItem?> PopNextAsync(ulong guildId);
    Task<PlayQueueItem?> PeekNextAsync(ulong guildId);
    Task InsertAtFrontAsync(ulong guildId, PlayQueueItem item);
    Task ShuffleAsync(ulong guildId);
    Task ClearAsync(ulong guildId);
    Task<IReadOnlyList<PlayQueueItem>> GetPageAsync(ulong guildId, int skip, int take);
    Task<int> GetCountAsync(ulong guildId);
}
