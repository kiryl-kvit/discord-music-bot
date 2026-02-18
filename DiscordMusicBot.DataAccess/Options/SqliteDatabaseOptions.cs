namespace DiscordMusicBot.DataAccess.Options;

public sealed class SqliteDatabaseOptions
{
    public const string SectionName = "SqliteDatabase";

    public required string DbFilePath { get; init; } = null!;
}
