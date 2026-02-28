using Dapper;
using DiscordMusicBot.Domain.Settings;
using DiscordMusicBot.Infrastructure.Database;
using DiscordMusicBot.Infrastructure.Dto;

namespace DiscordMusicBot.Infrastructure.Repositories;

public sealed class GuildSettingsRepository(SqliteConnectionFactory connectionFactory) : IGuildSettingsRepository
{
    public async Task<GuildSettings?> GetAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        var row = await connection.QueryFirstOrDefaultAsync<GuildSettingsRow>(
            new CommandDefinition(
                "SELECT guild_id, autoplay_enabled FROM guild_settings WHERE guild_id = @GuildId",
                new { GuildId = guildId.ToString() },
                cancellationToken: cancellationToken));

        return row?.ToGuildSettings();
    }

    public async Task SetAutoplayAsync(ulong guildId, bool enabled,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO guild_settings (guild_id, autoplay_enabled)
                VALUES (@GuildId, @Enabled)
                ON CONFLICT(guild_id) DO UPDATE SET autoplay_enabled = @Enabled
                """,
                new { GuildId = guildId.ToString(), Enabled = enabled ? 1 : 0 },
                cancellationToken: cancellationToken));
    }
}
