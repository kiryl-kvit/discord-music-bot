namespace DiscordMusicBot.Domain.Settings;

public sealed class GuildSettings
{
    public ulong GuildId { get; private set; }
    public bool AutoplayEnabled { get; private set; }

    private GuildSettings()
    {
    }

    public static GuildSettings Create(ulong guildId, bool autoplayEnabled = false)
    {
        return new GuildSettings
        {
            GuildId = guildId,
            AutoplayEnabled = autoplayEnabled
        };
    }

    public static GuildSettings Restore(ulong guildId, bool autoplayEnabled)
    {
        return new GuildSettings
        {
            GuildId = guildId,
            AutoplayEnabled = autoplayEnabled
        };
    }
}
