namespace DiscordMusicBot.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Path { get; set; } = "database.db";
}
