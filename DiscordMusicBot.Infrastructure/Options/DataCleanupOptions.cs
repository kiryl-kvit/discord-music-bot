namespace DiscordMusicBot.Infrastructure.Options;

public sealed class DataCleanupOptions
{
    public const string SectionName = "DataCleanup";

    public int RetentionDays { get; set; } = 2;

    public double IntervalHours { get; set; } = 12;
}
