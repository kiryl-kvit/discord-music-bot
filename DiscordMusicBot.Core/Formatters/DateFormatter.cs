namespace DiscordMusicBot.Core.Formatters;

public static class DateFormatter
{
    /// <summary>
    /// Formats a <see cref="TimeSpan"/> as a human-readable duration string.
    /// Hours are omitted when the duration is under one hour (e.g. "4:02"),
    /// and included otherwise (e.g. "1:04:02").
    /// </summary>
    public static string FormatTime(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
    }
}