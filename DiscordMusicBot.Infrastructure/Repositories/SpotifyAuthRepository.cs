using Dapper;
using DiscordMusicBot.Domain.Spotify;
using DiscordMusicBot.Infrastructure.Database;

namespace DiscordMusicBot.Infrastructure.Repositories;

public sealed class SpotifyAuthRepository(SqliteConnectionFactory connectionFactory) : ISpotifyAuthRepository
{
    public async Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT refresh_token FROM spotify_auth WHERE id = 1",
                cancellationToken: cancellationToken));
    }

    public async Task SaveRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO spotify_auth (id, refresh_token, updated_at)
                VALUES (1, @RefreshToken, datetime('now'))
                ON CONFLICT(id) DO UPDATE SET refresh_token = @RefreshToken, updated_at = datetime('now')
                """,
                new { RefreshToken = refreshToken },
                cancellationToken: cancellationToken));
    }
}
