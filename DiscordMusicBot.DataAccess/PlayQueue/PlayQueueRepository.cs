using DiscordMusicBot.Core;
using DiscordMusicBot.Domain.PlayQueue;
using DiscordMusicBot.Domain.PlayQueue.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.DataAccess.PlayQueue;

public sealed class PlayQueueRepository(MusicBotDbContext dbContext, ILogger<PlayQueueRepository> logger)
    : IPlayQueueRepository
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

            return Result<IReadOnlyList<PlayQueueItem>>.Success(items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            return Result<IReadOnlyList<PlayQueueItem>>.Failure(ex.Message);
        }
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
