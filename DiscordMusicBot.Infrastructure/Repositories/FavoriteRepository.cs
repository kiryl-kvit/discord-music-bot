using Dapper;
using DiscordMusicBot.Domain.Favorites;
using DiscordMusicBot.Infrastructure.Database;
using DiscordMusicBot.Infrastructure.Dto;

namespace DiscordMusicBot.Infrastructure.Repositories;

public sealed class FavoriteRepository(SqliteConnectionFactory connectionFactory) : IFavoriteRepository
{
    public async Task<long> AddAsync(FavoriteItem item, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var id = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                INSERT INTO favorite_items (user_id, url, title, alias, author, duration_ms, is_playlist)
                VALUES (@UserId, @Url, @Title, @Alias, @Author, @DurationMs, @IsPlaylist)
                RETURNING id
                """,
                new
                {
                    UserId = item.UserId.ToString(),
                    item.Url,
                    item.Title,
                    Alias = item.Alias,
                    item.Author,
                    DurationMs = item.Duration.HasValue ? (long?)item.Duration.Value.TotalMilliseconds : null,
                    IsPlaylist = item.IsPlaylist ? 1 : 0,
                },
                cancellationToken: cancellationToken));

        return id;
    }

    public async Task<bool> RemoveByIdAsync(long id, ulong userId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM favorite_items WHERE id = @Id AND user_id = @UserId",
                new { Id = id, UserId = userId.ToString() },
                cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> ExistsByUrlAsync(ulong userId, string url, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM favorite_items WHERE user_id = @UserId AND url = @Url",
                new { UserId = userId.ToString(), Url = url },
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<FavoriteItem?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var row = await connection.QueryFirstOrDefaultAsync<FavoriteItemRow>(
            new CommandDefinition(
                """
                SELECT id, user_id, url, title, alias, author, duration_ms, is_playlist, created_at
                FROM favorite_items
                WHERE id = @Id
                """,
                new { Id = id },
                cancellationToken: cancellationToken));

        return row?.ToFavoriteItem();
    }

    public async Task<IReadOnlyList<FavoriteItem>> GetByUserAsync(ulong userId, int skip, int take,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<FavoriteItemRow>(
            new CommandDefinition(
                """
                SELECT id, user_id, url, title, alias, author, duration_ms, is_playlist, created_at
                FROM favorite_items
                WHERE user_id = @UserId
                ORDER BY created_at DESC
                LIMIT @Take OFFSET @Skip
                """,
                new { UserId = userId.ToString(), Take = take, Skip = skip },
                cancellationToken: cancellationToken));

        return rows.Select(r => r.ToFavoriteItem()).ToArray();
    }

    public async Task<int> GetCountAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM favorite_items WHERE user_id = @UserId",
                new { UserId = userId.ToString() },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<FavoriteItem>> SearchAsync(ulong userId, string query, int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<FavoriteItemRow>(
            new CommandDefinition(
                """
                SELECT id, user_id, url, title, alias, author, duration_ms, is_playlist, created_at
                FROM favorite_items
                WHERE user_id = @UserId
                  AND (title LIKE @Query OR alias LIKE @Query OR author LIKE @Query)
                ORDER BY
                  CASE WHEN alias LIKE @Query THEN 0
                       WHEN title LIKE @Query THEN 1
                       ELSE 2
                  END,
                  created_at DESC
                LIMIT @Limit
                """,
                new { UserId = userId.ToString(), Query = $"%{query}%", Limit = limit },
                cancellationToken: cancellationToken));

        return rows.Select(r => r.ToFavoriteItem()).ToArray();
    }
}
