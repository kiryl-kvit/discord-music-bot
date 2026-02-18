using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.EntityFrameworkCore;

namespace DiscordMusicBot.DataAccess.PlayQueue;

public sealed class PlayQueueRepository(MusicBotDbContext dbContext) : IPlayQueueRepository
{
    public async Task<IReadOnlyList<PlayQueueItem>> GetAllAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        return await dbContext.PlayQueueItems
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task<PlayQueueItem> EnqueueAsync(
        ulong guildId,
        PlayQueueItemType type,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Source is required.", nameof(source));
        }

        var lastPosition = await dbContext.PlayQueueItems
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.Position)
            .Select(x => (long?)x.Position)
            .FirstOrDefaultAsync(cancellationToken);

        var nextPosition = lastPosition.GetValueOrDefault() + 1;

        var item = new PlayQueueItem
        {
            GuildId = guildId,
            Type = type,
            Source = source.Trim(),
            Position = nextPosition,
            EnqueuedAtUtc = DateTime.UtcNow,
        };

        dbContext.PlayQueueItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return item;
    }

    public async Task<PlayQueueItem?> DequeueAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.PlayQueueItems
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Position)
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return null;
        }

        dbContext.PlayQueueItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return item;
    }

    public async Task ClearAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        await dbContext.PlayQueueItems
            .Where(x => x.GuildId == guildId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
