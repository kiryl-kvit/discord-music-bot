using Dapper;
using DiscordMusicBot.Domain.PlayQueue;
using DiscordMusicBot.Infrastructure.Database;
using DiscordMusicBot.Infrastructure.Dto;

namespace DiscordMusicBot.Infrastructure.Repositories;

public sealed class PlayQueueRepository(SqliteConnectionFactory connectionFactory) : IPlayQueueRepository
{
    public async Task AddItemsAsync(ulong guildId, IReadOnlyList<PlayQueueItem> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var maxPosition = await connection.ExecuteScalarAsync<int?>(
            "SELECT MAX(position) FROM play_queue_items WHERE guild_id = @GuildId",
            new { GuildId = guildId.ToString() },
            transaction);

        var nextPosition = (maxPosition ?? -1) + 1;

        foreach (var item in items)
        {
            var id = await connection.ExecuteScalarAsync<long>(
                """
                INSERT INTO play_queue_items (guild_id, user_id, url, title, author, duration_ms, position)
                VALUES (@GuildId, @UserId, @Url, @Title, @Author, @DurationMs, @Position);
                SELECT last_insert_rowid();
                """,
                new
                {
                    GuildId = guildId.ToString(),
                    UserId = item.UserId.ToString(),
                    item.Url,
                    item.Title,
                    item.Author,
                    DurationMs = item.Duration.HasValue ? (long?)item.Duration.Value.TotalMilliseconds : null,
                    Position = nextPosition++,
                },
                transaction);

            item.SetId(id);
        }

        await transaction.CommitAsync();
    }

    public async Task<PlayQueueItem?> PeekNextAsync(ulong guildId, int skip = 0)
    {
        await using var connection = connectionFactory.CreateConnection();

        var row = await connection.QueryFirstOrDefaultAsync<PlayQueueItemRow>(
            """
            SELECT id, guild_id, user_id, url, title, author, duration_ms, position
            FROM play_queue_items
            WHERE guild_id = @GuildId
            ORDER BY position
            LIMIT 1 OFFSET @Skip
            """,
            new { GuildId = guildId.ToString(), Skip = skip });

        return row?.ToPlayQueueItem();
    }

    public async Task DeleteByIdAsync(ulong guildId, long itemId)
    {
        await using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "DELETE FROM play_queue_items WHERE id = @Id AND guild_id = @GuildId",
            new { Id = itemId, GuildId = guildId.ToString() });
    }

    public async Task ShuffleAsync(ulong guildId, long? excludeItemId = null)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var ids = (await connection.QueryAsync<long>(
            """
            SELECT id FROM play_queue_items
            WHERE guild_id = @GuildId AND (@ExcludeId IS NULL OR id != @ExcludeId)
            ORDER BY position
            """,
            new { GuildId = guildId.ToString(), ExcludeId = excludeItemId },
            transaction)).ToList();

        if (ids.Count <= 1)
        {
            return;
        }

        // Fisher-Yates shuffle
        for (var i = ids.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(0, i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }

        var positionOffset = excludeItemId.HasValue ? 1 : 0;

        for (var i = 0; i < ids.Count; i++)
        {
            await connection.ExecuteAsync(
                "UPDATE play_queue_items SET position = @Position WHERE id = @Id",
                new { Position = i + positionOffset, Id = ids[i] },
                transaction);
        }

        await transaction.CommitAsync();
    }

    public async Task ClearAsync(ulong guildId)
    {
        await using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "DELETE FROM play_queue_items WHERE guild_id = @GuildId",
            new { GuildId = guildId.ToString() });
    }

    public async Task<IReadOnlyList<PlayQueueItem>> GetPageAsync(ulong guildId, int skip, int take)
    {
        await using var connection = connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<PlayQueueItemRow>(
            """
            SELECT id, guild_id, user_id, url, title, author, duration_ms, position
            FROM play_queue_items
            WHERE guild_id = @GuildId
            ORDER BY position
            LIMIT @Take OFFSET @Skip
            """,
            new { GuildId = guildId.ToString(), Take = take, Skip = skip });

        return rows.Select(r => r.ToPlayQueueItem()).ToArray();
    }

    public async Task<int> GetCountAsync(ulong guildId)
    {
        await using var connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM play_queue_items WHERE guild_id = @GuildId",
            new { GuildId = guildId.ToString() });
    }
}
