namespace DiscordMusicBot.Domain.Playback;

public interface IGuildPlaybackStateRepository
{
    Task SaveAsync(PersistedGuildState state);
    Task<PersistedGuildState?> GetAsync(ulong guildId);
    Task<IReadOnlyList<PersistedGuildState>> GetAllAsync();
    Task DeleteAsync(ulong guildId);
}
