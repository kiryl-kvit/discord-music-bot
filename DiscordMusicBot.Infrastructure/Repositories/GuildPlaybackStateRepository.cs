using Dapper;
using DiscordMusicBot.Domain.Playback;
using DiscordMusicBot.Infrastructure.Database;
using DiscordMusicBot.Infrastructure.Dto;

namespace DiscordMusicBot.Infrastructure.Repositories;

public sealed class GuildPlaybackStateRepository(SqliteConnectionFactory connectionFactory)
    : IGuildPlaybackStateRepository
{
    public async Task SaveAsync(PersistedGuildState state)
    {
        await using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            """
            INSERT INTO guild_playback_state (guild_id, voice_channel_id, feedback_channel_id, resume_position_ms, resume_item_id, updated_at)
            VALUES (@GuildId, @VoiceChannelId, @FeedbackChannelId, @ResumePositionMs, @ResumeItemId, datetime('now'))
            ON CONFLICT(guild_id) DO UPDATE SET
                voice_channel_id    = @VoiceChannelId,
                feedback_channel_id = @FeedbackChannelId,
                resume_position_ms  = @ResumePositionMs,
                resume_item_id      = @ResumeItemId,
                updated_at          = datetime('now')
            """,
            new
            {
                GuildId = state.GuildId.ToString(),
                VoiceChannelId = state.VoiceChannelId.ToString(),
                FeedbackChannelId = state.FeedbackChannelId?.ToString(),
                ResumePositionMs = (long)state.ResumePosition.TotalMilliseconds,
                state.ResumeItemId,
            });
    }

    public async Task<PersistedGuildState?> GetAsync(ulong guildId)
    {
        await using var connection = connectionFactory.CreateConnection();

        var row = await connection.QueryFirstOrDefaultAsync<GuildPlaybackStateRow>(
            """
            SELECT guild_id, voice_channel_id, feedback_channel_id, resume_position_ms, resume_item_id
            FROM guild_playback_state
            WHERE guild_id = @GuildId
            """,
            new { GuildId = guildId.ToString() });

        return row?.ToPersistedGuildState();
    }

    public async Task<IReadOnlyList<PersistedGuildState>> GetAllAsync()
    {
        await using var connection = connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<GuildPlaybackStateRow>(
            "SELECT guild_id, voice_channel_id, feedback_channel_id, resume_position_ms, resume_item_id FROM guild_playback_state");

        return rows.Select(r => r.ToPersistedGuildState()).ToArray();
    }

    public async Task DeleteAsync(ulong guildId)
    {
        await using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "DELETE FROM guild_playback_state WHERE guild_id = @GuildId",
            new { GuildId = guildId.ToString() });
    }
}
