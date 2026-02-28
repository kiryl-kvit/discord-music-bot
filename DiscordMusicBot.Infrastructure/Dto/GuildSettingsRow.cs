using DiscordMusicBot.Domain.Settings;

namespace DiscordMusicBot.Infrastructure.Dto;

internal sealed class GuildSettingsRow
{
    public string GuildId { get; init; } = null!;
    public int AutoplayEnabled { get; init; }

    public GuildSettings ToGuildSettings()
    {
        return GuildSettings.Restore(
            ulong.Parse(GuildId),
            AutoplayEnabled != 0);
    }
}
