namespace DiscordMusicBot.Core.Formatters;

public static class DisplayConstants
{
    public const string UnknownAuthor = "Unknown";
    public const string UnknownDuration = "??:??";

    public static string AuthorOrDefault(string? author) =>
        string.IsNullOrWhiteSpace(author) ? UnknownAuthor : author;

    public static string FormatAutoplayStatus(bool enabled) =>
        $"Autoplay: {(enabled ? "on" : "off")}";
}
