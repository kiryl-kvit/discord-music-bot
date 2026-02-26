namespace DiscordMusicBot.Core.Formatters;

public static class DiscordFormatter
{
    public static string MentionUser(ulong userId) => $"<@{userId}>";
}
