using System.Text;
using Dapper;
using DiscordMusicBot.Domain.Playlists;
using DiscordMusicBot.Infrastructure.Database;
using DiscordMusicBot.Infrastructure.Dto;

namespace DiscordMusicBot.Infrastructure.Repositories;

public sealed class PlaylistRepository(SqliteConnectionFactory connectionFactory) : IPlaylistRepository
{
    public async Task<long> CreateAsync(Playlist playlist, IReadOnlyList<PlaylistItem> items,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var playlistId = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                INSERT INTO playlists (user_id, name, track_count, total_duration_ms)
                VALUES (@UserId, @Name, @TrackCount, @TotalDurationMs)
                RETURNING id
                """,
                new
                {
                    UserId = playlist.UserId.ToString(),
                    playlist.Name,
                    playlist.TrackCount,
                    playlist.TotalDurationMs,
                },
                transaction: transaction,
                cancellationToken: cancellationToken));

        if (items.Count > 0)
        {
            var sb = new StringBuilder();
            sb.Append("INSERT INTO playlist_items (playlist_id, position, url, title, author, duration_ms, thumbnail_url) VALUES ");

            var parameters = new DynamicParameters();
            parameters.Add("PlaylistId", playlistId);

            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"(@PlaylistId, @Position{i}, @Url{i}, @Title{i}, @Author{i}, @DurationMs{i}, @ThumbnailUrl{i})");

                var item = items[i];
                parameters.Add($"Position{i}", item.Position);
                parameters.Add($"Url{i}", item.Url);
                parameters.Add($"Title{i}", item.Title);
                parameters.Add($"Author{i}", item.Author);
                parameters.Add($"DurationMs{i}", item.DurationMs);
                parameters.Add($"ThumbnailUrl{i}", item.ThumbnailUrl);
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    sb.ToString(), parameters, transaction: transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        return playlistId;
    }

    public async Task<bool> DeleteAsync(long playlistId, ulong userId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM playlists WHERE id = @Id AND user_id = @UserId",
                new { Id = playlistId, UserId = userId.ToString() },
                cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> RenameAsync(long playlistId, ulong userId, string newName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE playlists SET name = @Name, updated_at = datetime('now') WHERE id = @Id AND user_id = @UserId",
                new { Name = newName, Id = playlistId, UserId = userId.ToString() },
                cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<Playlist?> GetByIdAsync(long playlistId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var row = await connection.QueryFirstOrDefaultAsync<PlaylistRow>(
            new CommandDefinition(
                """
                SELECT id, user_id, name, track_count, total_duration_ms, created_at, updated_at
                FROM playlists
                WHERE id = @Id
                """,
                new { Id = playlistId },
                cancellationToken: cancellationToken));

        return row?.ToPlaylist();
    }

    public async Task<IReadOnlyList<Playlist>> GetByUserAsync(ulong userId, int skip, int take,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<PlaylistRow>(
            new CommandDefinition(
                """
                SELECT id, user_id, name, track_count, total_duration_ms, created_at, updated_at
                FROM playlists
                WHERE user_id = @UserId
                ORDER BY updated_at DESC
                LIMIT @Take OFFSET @Skip
                """,
                new { UserId = userId.ToString(), Take = take, Skip = skip },
                cancellationToken: cancellationToken));

        return rows.Select(r => r.ToPlaylist()).ToArray();
    }

    public async Task<int> GetCountAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM playlists WHERE user_id = @UserId",
                new { UserId = userId.ToString() },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PlaylistItem>> GetItemsAsync(long playlistId, int skip, int take,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<PlaylistItemRow>(
            new CommandDefinition(
                """
                SELECT id, playlist_id, position, url, title, author, duration_ms, thumbnail_url
                FROM playlist_items
                WHERE playlist_id = @PlaylistId
                ORDER BY position
                LIMIT @Take OFFSET @Skip
                """,
                new { PlaylistId = playlistId, Take = take, Skip = skip },
                cancellationToken: cancellationToken));

        return rows.Select(r => r.ToPlaylistItem()).ToArray();
    }

    public async Task<IReadOnlyList<PlaylistItem>> GetAllItemsAsync(long playlistId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<PlaylistItemRow>(
            new CommandDefinition(
                """
                SELECT id, playlist_id, position, url, title, author, duration_ms, thumbnail_url
                FROM playlist_items
                WHERE playlist_id = @PlaylistId
                ORDER BY position
                """,
                new { PlaylistId = playlistId },
                cancellationToken: cancellationToken));

        return rows.Select(r => r.ToPlaylistItem()).ToArray();
    }

    public async Task<IReadOnlyList<Playlist>> SearchAsync(ulong userId, string query, int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<PlaylistRow>(
            new CommandDefinition(
                """
                SELECT id, user_id, name, track_count, total_duration_ms, created_at, updated_at
                FROM playlists
                WHERE user_id = @UserId
                  AND name LIKE @Query ESCAPE '\'
                ORDER BY updated_at DESC
                LIMIT @Limit
                """,
                new
                {
                    UserId = userId.ToString(),
                    Query = $"%{EscapeLikePattern(query)}%",
                    Limit = limit,
                },
                cancellationToken: cancellationToken));

        return rows.Select(r => r.ToPlaylist()).ToArray();
    }

    public async Task<bool> ExistsByNameAsync(ulong userId, string name,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM playlists WHERE user_id = @UserId AND name = @Name",
                new { UserId = userId.ToString(), Name = name },
                cancellationToken: cancellationToken));

        return count > 0;
    }

    private static string EscapeLikePattern(string input)
    {
        return input
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
    }
}
