namespace DiscordMusicBot.Domain.PlayQueue;

public interface IPlayQueueRepository
{
    Task<IReadOnlyList<PlayQueueItem>> GetAllAsync(ulong guildId, CancellationToken cancellationToken = default);

    Task<PlayQueueItem> EnqueueAsync(
        ulong guildId,
        PlayQueueItemType type,
        string source,
        CancellationToken cancellationToken = default);

    Task<PlayQueueItem?> DequeueAsync(ulong guildId, CancellationToken cancellationToken = default);

    Task ClearAsync(ulong guildId, CancellationToken cancellationToken = default);
}
