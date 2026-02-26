using System.Text;
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
        var guildIdStr = guildId.ToString();

        var sb = new StringBuilder();
        sb.Append("INSERT INTO play_queue_items (guild_id, user_id, url, title, author, duration_ms, position) VALUES ");

        var parameters = new DynamicParameters();
        parameters.Add("GuildId", guildIdStr);

        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"(@GuildId, @UserId{i}, @Url{i}, @Title{i}, @Author{i}, @DurationMs{i}, @Position{i})");

            var item = items[i];
            parameters.Add($"UserId{i}", item.UserId.ToString());
            parameters.Add($"Url{i}", item.Url);
            parameters.Add($"Title{i}", item.Title);
            parameters.Add($"Author{i}", item.Author);
            parameters.Add($"DurationMs{i}", item.Duration.HasValue ? (long?)item.Duration.Value.TotalMilliseconds : null);
            parameters.Add($"Position{i}", nextPosition + i);
        }

        await connection.ExecuteAsync(sb.ToString(), parameters, transaction);

        var ids = (await connection.QueryAsync<long>(
            """
            SELECT id FROM play_queue_items
            WHERE guild_id = @GuildId AND position >= @StartPosition
            ORDER BY position
            """,
            new { GuildId = guildIdStr, StartPosition = nextPosition },
            transaction)).AsList();

        for (var i = 0; i < items.Count; i++)
        {
            items[i].SetId(ids[i]);
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

        var sb = new StringBuilder();
        sb.Append("UPDATE play_queue_items SET position = CASE id ");

        var parameters = new DynamicParameters();
        parameters.Add("GuildId", guildId.ToString());

        for (var i = 0; i < ids.Count; i++)
        {
            sb.Append($"WHEN @Id{i} THEN @Pos{i} ");
            parameters.Add($"Id{i}", ids[i]);
            parameters.Add($"Pos{i}", i + positionOffset);
        }

        sb.Append("END WHERE guild_id = @GuildId AND id IN (");
        for (var i = 0; i < ids.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"@Id{i}");
        }

        sb.Append(')');

        await connection.ExecuteAsync(sb.ToString(), parameters, transaction);

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
