using DiscordMusicBot.Core;
using DiscordMusicBot.Domain.PlayQueue.Dto;

namespace DiscordMusicBot.Domain.PlayQueue;

public interface IPlayQueueRepository
{
    Task<IReadOnlyList<PlayQueueItem>> GetAllAsync(ulong guildId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PlayQueueItem>>> EnqueueAsync(IEnumerable<EnqueueItemDto> dtos,
        CancellationToken cancellationToken = default);

    Task<PlayQueueItem?> DequeueAsync(ulong guildId, CancellationToken cancellationToken = default);

    Task ClearAsync(ulong guildId, CancellationToken cancellationToken = default);
}
