using DiscordMusicBot.Core.MusicSource;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.Domain.History;

public interface IHistoryRepository
{
    Task<IReadOnlyList<PlayQueueItem>> GetPageAsync(ulong guildId, int skip, int take, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(ulong guildId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRecentUrlsAsync(ulong guildId, int limit, CancellationToken cancellationToken = default);
    Task<PlayQueueItem?> GetLastPlayedAsync(ulong guildId, CancellationToken cancellationToken = default);
    Task<PlayQueueItem?> GetLastPlayedBySourceTypesAsync(ulong guildId, IReadOnlyList<SourceType> sourceTypes, CancellationToken cancellationToken = default);
    Task<PlayQueueItem?> GetByIdAsync(ulong guildId, long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayQueueItem>> SearchAsync(ulong guildId, string query, int limit, CancellationToken cancellationToken = default);
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
