namespace DiscordMusicBot.Domain.Settings;

public sealed class GuildSettings
{
    public const int DefaultAutoplayQueueSize = 25;

    public ulong GuildId { get; private set; }
    public bool AutoplayEnabled { get; private set; }
    public int AutoplayQueueSize { get; private set; }

    private GuildSettings()
    {
    }

    public static GuildSettings Create(ulong guildId, bool autoplayEnabled = false,
        int autoplayQueueSize = DefaultAutoplayQueueSize)
    {
        return new GuildSettings
        {
            GuildId = guildId,
            AutoplayEnabled = autoplayEnabled,
            AutoplayQueueSize = autoplayQueueSize
        };
    }

    public static GuildSettings Restore(ulong guildId, bool autoplayEnabled, int? autoplayQueueSize)
    {
        return new GuildSettings
        {
            GuildId = guildId,
            AutoplayEnabled = autoplayEnabled,
            AutoplayQueueSize = autoplayQueueSize ?? DefaultAutoplayQueueSize
        };
    }
}
