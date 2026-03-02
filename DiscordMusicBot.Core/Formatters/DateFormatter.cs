namespace DiscordMusicBot.Core.Formatters;

public static class DateFormatter
{
    public static string FormatTime(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
    }

    public static string FormatTimeOrDefault(TimeSpan? duration)
    {
        return duration is not null
            ? FormatTime(duration.Value)
            : DisplayConstants.UnknownDuration;
    }
}