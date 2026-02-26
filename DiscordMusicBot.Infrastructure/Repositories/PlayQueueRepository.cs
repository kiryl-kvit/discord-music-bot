using System.Text;
using Dapper;
using DiscordMusicBot.Domain.PlayQueue;
using DiscordMusicBot.Infrastructure.Database;
using DiscordMusicBot.Infrastructure.Dto;

namespace DiscordMusicBot.Infrastructure.Repositories;

public sealed class PlayQueueRepository(SqliteConnectionFactory connectionFactory) : IPlayQueueRepository
{
    public async Task AddItemsAsync(ulong guildId, IReadOnlyList<PlayQueueItem> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var maxPosition = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT MAX(position) FROM play_queue_items WHERE guild_id = @GuildId",
                new { GuildId = guildId.ToString() },
                transaction: transaction,
                cancellationToken: cancellationToken));

        var nextPosition = (maxPosition ?? -1) + 1;
        var guildIdStr = guildId.ToString();

        var sb = new StringBuilder();
        sb.Append("INSERT INTO play_queue_items (guild_id, user_id, url, title, author, duration_ms, thumbnail_url, position) VALUES ");

        var parameters = new DynamicParameters();
        parameters.Add("GuildId", guildIdStr);

        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append($"(@GuildId, @UserId{i}, @Url{i}, @Title{i}, @Author{i}, @DurationMs{i}, @ThumbnailUrl{i}, @Position{i})");

            var item = items[i];
            parameters.Add($"UserId{i}", item.UserId.ToString());
            parameters.Add($"Url{i}", item.Url);
            parameters.Add($"Title{i}", item.Title);
            parameters.Add($"Author{i}", item.Author);
            parameters.Add($"DurationMs{i}", item.Duration.HasValue ? (long?)item.Duration.Value.TotalMilliseconds : null);
            parameters.Add($"ThumbnailUrl{i}", item.ThumbnailUrl);
            parameters.Add($"Position{i}", nextPosition + i);
        }

        sb.Append(" RETURNING id");

        var ids = (await connection.QueryAsync<long>(
            new CommandDefinition(
                sb.ToString(), parameters, transaction: transaction, cancellationToken: cancellationToken))).AsList();

        for (var i = 0; i < items.Count; i++)
        {
            items[i].SetId(ids[i]);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PlayQueueItem?> PeekNextAsync(ulong guildId, int skip = 0,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var row = await connection.QueryFirstOrDefaultAsync<PlayQueueItemRow>(
            new CommandDefinition(
                """
                SELECT id, guild_id, user_id, url, title, author, duration_ms, thumbnail_url, position
                FROM play_queue_items
                WHERE guild_id = @GuildId
                ORDER BY position
                LIMIT 1 OFFSET @Skip
                """,
                new { GuildId = guildId.ToString(), Skip = skip },
                cancellationToken: cancellationToken));

        return row?.ToPlayQueueItem();
    }

    public async Task DeleteByIdAsync(ulong guildId, long itemId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM play_queue_items WHERE id = @Id AND guild_id = @GuildId",
                new { Id = itemId, GuildId = guildId.ToString() },
                cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteTopNAsync(ulong guildId, int count, long? excludeItemId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var deleted = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM play_queue_items
                WHERE id IN (
                    SELECT id FROM play_queue_items
                    WHERE guild_id = @GuildId AND (@ExcludeId IS NULL OR id != @ExcludeId)
                    ORDER BY position
                    LIMIT @Count
                )
                """,
                new { GuildId = guildId.ToString(), Count = count, ExcludeId = excludeItemId },
                cancellationToken: cancellationToken));

        return deleted;
    }

    public async Task ShuffleAsync(ulong guildId, long? excludeItemId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var ids = (await connection.QueryAsync<long>(
            new CommandDefinition(
                """
                SELECT id FROM play_queue_items
                WHERE guild_id = @GuildId AND (@ExcludeId IS NULL OR id != @ExcludeId)
                ORDER BY position
                """,
                new { GuildId = guildId.ToString(), ExcludeId = excludeItemId },
                cancellationToken: cancellationToken))).ToList();

        if (ids.Count <= 1)
        {
            return;
        }

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
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append($"@Id{i}");
        }

        sb.Append(')');

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                sb.ToString(), parameters, transaction: transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ClearAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM play_queue_items WHERE guild_id = @GuildId",
                new { GuildId = guildId.ToString() },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PlayQueueItem>> GetPageAsync(ulong guildId, int skip, int take,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<PlayQueueItemRow>(
            new CommandDefinition(
                """
                SELECT id, guild_id, user_id, url, title, author, duration_ms, thumbnail_url, position
                FROM play_queue_items
                WHERE guild_id = @GuildId
                ORDER BY position
                LIMIT @Take OFFSET @Skip
                """,
                new { GuildId = guildId.ToString(), Take = take, Skip = skip },
                cancellationToken: cancellationToken));

        return rows.Select(r => r.ToPlayQueueItem()).ToArray();
    }

    public async Task<int> GetCountAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM play_queue_items WHERE guild_id = @GuildId",
                new { GuildId = guildId.ToString() },
                cancellationToken: cancellationToken));
    }

    public async Task<(int Count, long TotalDurationMs)> GetCountAndTotalDurationMsAsync(ulong guildId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var row = await connection.QuerySingleAsync<(int Count, long TotalDurationMs)>(
            new CommandDefinition(
                "SELECT COUNT(*), COALESCE(SUM(duration_ms), 0) FROM play_queue_items WHERE guild_id = @GuildId",
                new { GuildId = guildId.ToString() },
                cancellationToken: cancellationToken));

        return row;
    }
}
