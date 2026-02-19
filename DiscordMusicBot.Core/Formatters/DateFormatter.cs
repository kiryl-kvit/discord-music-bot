namespace DiscordMusicBot.Core.Formatters;

public static class DateFormatter
{
    public static string FormatTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"mm\:ss");
    }
}