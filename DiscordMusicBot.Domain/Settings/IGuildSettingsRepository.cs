namespace DiscordMusicBot.Domain.Settings;

public interface IGuildSettingsRepository
{
    Task<GuildSettings?> GetAsync(ulong guildId, CancellationToken cancellationToken = default);
    Task SetAutoplayAsync(ulong guildId, bool enabled, CancellationToken cancellationToken = default);
    Task SetAutoplayQueueSizeAsync(ulong guildId, int queueSize, CancellationToken cancellationToken = default);
}
