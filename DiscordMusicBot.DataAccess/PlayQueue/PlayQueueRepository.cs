using DiscordMusicBot.Core;
using DiscordMusicBot.Domain.PlayQueue;
using DiscordMusicBot.Domain.PlayQueue.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.DataAccess.PlayQueue;

public sealed class PlayQueueRepository(
    MusicBotDbContext dbContext,
    IEnumerable<IPlayQueueEventListener> eventListeners,
    ILogger<PlayQueueRepository> logger) : IPlayQueueRepository
{
    public async Task<IReadOnlyList<PlayQueueItem>> GetAllAsync(ulong guildId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PlayQueueItems
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<IReadOnlyList<PlayQueueItem>>> EnqueueAsync(IEnumerable<EnqueueItemDto> dtos,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dtoArray = dtos as EnqueueItemDto[] ?? dtos.ToArray();
            if (dtoArray.Length == 0)
            {
                return Result<IReadOnlyList<PlayQueueItem>>.Success([]);
            }

            var guildId = dtoArray[0].GuildId;
            var lastPosition = await dbContext.PlayQueueItems
                .AsNoTracking()
                .Where(x => x.GuildId == guildId)
                .OrderByDescending(x => x.Position)
                .Select(x => (long?)x.Position)
                .FirstOrDefaultAsync(cancellationToken);

            var nextPosition = lastPosition.GetValueOrDefault() + 1;
            var items = new PlayQueueItem[dtoArray.Length];
            for (var index = 0; index < dtoArray.Length; index++)
            {
                var dto = dtoArray[index];
                var item = PlayQueueItem.Create(dto.GuildId, dto.UserId, dto.Url, dto.Title, nextPosition, dto.Author,
                    dto.Duration);
                items[index] = item;
                nextPosition++;
            }

            dbContext.PlayQueueItems.AddRange(items);
            await dbContext.SaveChangesAsync(cancellationToken);

            await NotifyItemsAddedAsync(guildId, items);

            return Result<IReadOnlyList<PlayQueueItem>>.Success(items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue items");
            return Result<IReadOnlyList<PlayQueueItem>>.Failure(ex.Message);
        }
    }

    public async Task<PlayQueueItem?> PeekAsync(ulong guildId, int skip = 0,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PlayQueueItems
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Position)
            .Skip(skip)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task RemoveAsync(long itemId, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.PlayQueueItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);

        await dbContext.PlayQueueItems
            .Where(x => x.Id == itemId)
            .ExecuteDeleteAsync(cancellationToken);

        if (item is not null)
        {
            await NotifyItemsRemovedAsync(item.GuildId, [item]);
        }
    }

    public async Task ClearAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        var items = await dbContext.PlayQueueItems
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Position)
            .ToListAsync(cancellationToken);

        await dbContext.PlayQueueItems
            .Where(x => x.GuildId == guildId)
            .ExecuteDeleteAsync(cancellationToken);

        if (items.Count > 0)
        {
            await NotifyItemsRemovedAsync(guildId, items);
        }
    }

    private async Task NotifyItemsAddedAsync(ulong guildId, IReadOnlyList<PlayQueueItem> items)
    {
        foreach (var listener in eventListeners)
        {
            try
            {
                await listener.OnItemsAddedAsync(guildId, items);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Event listener {Listener} failed handling OnItemsAdded for guild {GuildId}",
                    listener.GetType().Name, guildId);
            }
        }
    }

    private async Task NotifyItemsRemovedAsync(ulong guildId, IReadOnlyList<PlayQueueItem> items)
    {
        foreach (var listener in eventListeners)
        {
            try
            {
                await listener.OnItemsRemovedAsync(guildId, items);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Event listener {Listener} failed handling OnItemsRemoved for guild {GuildId}",
                    listener.GetType().Name, guildId);
            }
        }
    }
}
