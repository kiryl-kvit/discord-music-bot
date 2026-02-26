namespace DiscordMusicBot.Domain.Playback;

public interface IGuildPlaybackStateRepository
{
    Task SaveAsync(PersistedGuildState state, CancellationToken cancellationToken = default);
    Task<PersistedGuildState?> GetAsync(ulong guildId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PersistedGuildState>> GetAllAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(ulong guildId, CancellationToken cancellationToken = default);
}
