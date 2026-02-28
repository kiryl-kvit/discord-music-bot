using System.Text;
using Dapper;
using DiscordMusicBot.Core.MusicSource;
using DiscordMusicBot.Domain.History;
using DiscordMusicBot.Domain.PlayQueue;
using DiscordMusicBot.Infrastructure.Database;
using DiscordMusicBot.Infrastructure.Dto;

namespace DiscordMusicBot.Infrastructure.Repositories;

public sealed class HistoryRepository(SqliteConnectionFactory connectionFactory) : IHistoryRepository
{
    public async Task<IReadOnlyList<PlayQueueItem>> GetPageAsync(ulong guildId, int skip, int take,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<PlayQueueItemRow>(
            new CommandDefinition(
                """
                SELECT id, guild_id, user_id, source_type, url, title, author, duration_ms, thumbnail_url, position, played_at
                FROM play_queue_items
                WHERE guild_id = @GuildId AND played_at IS NOT NULL
                ORDER BY played_at DESC
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
                "SELECT COUNT(*) FROM play_queue_items WHERE guild_id = @GuildId AND played_at IS NOT NULL",
                new { GuildId = guildId.ToString() },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<string>> GetRecentUrlsAsync(ulong guildId, int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var urls = await connection.QueryAsync<string>(
            new CommandDefinition(
                """
                SELECT url FROM play_queue_items
                WHERE guild_id = @GuildId AND played_at IS NOT NULL
                ORDER BY played_at DESC
                LIMIT @Limit
                """,
                new { GuildId = guildId.ToString(), Limit = limit },
                cancellationToken: cancellationToken));

        return urls.ToArray();
    }

    public async Task<PlayQueueItem?> GetLastPlayedAsync(ulong guildId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var row = await connection.QueryFirstOrDefaultAsync<PlayQueueItemRow>(
            new CommandDefinition(
                """
                SELECT id, guild_id, user_id, source_type, url, title, author, duration_ms, thumbnail_url, position, played_at
                FROM play_queue_items
                WHERE guild_id = @GuildId AND played_at IS NOT NULL
                ORDER BY played_at DESC
                LIMIT 1
                """,
                new { GuildId = guildId.ToString() },
                cancellationToken: cancellationToken));

        return row?.ToPlayQueueItem();
    }

    public async Task<PlayQueueItem?> GetLastPlayedBySourceTypesAsync(ulong guildId,
        IReadOnlyList<SourceType> sourceTypes, CancellationToken cancellationToken = default)
    {
        if (sourceTypes.Count == 0)
        {
            return null;
        }

        await using var connection = connectionFactory.CreateConnection();

        var sb = new StringBuilder();
        sb.Append("""
            SELECT id, guild_id, user_id, source_type, url, title, author, duration_ms, thumbnail_url, position, played_at
            FROM play_queue_items
            WHERE guild_id = @GuildId AND played_at IS NOT NULL
              AND source_type IN (
            """);

        var parameters = new DynamicParameters();
        parameters.Add("GuildId", guildId.ToString());

        for (var i = 0; i < sourceTypes.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append($"@SourceType{i}");
            parameters.Add($"SourceType{i}", sourceTypes[i].ToString());
        }

        sb.Append("""
            )
            ORDER BY played_at DESC
            LIMIT 1
            """);

        var row = await connection.QueryFirstOrDefaultAsync<PlayQueueItemRow>(
            new CommandDefinition(sb.ToString(), parameters, cancellationToken: cancellationToken));

        return row?.ToPlayQueueItem();
    }

    public async Task TrimAsync(ulong guildId, int keepCount,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM play_queue_items
                WHERE guild_id = @GuildId AND played_at IS NOT NULL
                  AND id NOT IN (
                      SELECT id FROM play_queue_items
                      WHERE guild_id = @GuildId AND played_at IS NOT NULL
                      ORDER BY played_at DESC
                      LIMIT @KeepCount
                  )
                """,
                new { GuildId = guildId.ToString(), KeepCount = keepCount },
                cancellationToken: cancellationToken));
    }
}
