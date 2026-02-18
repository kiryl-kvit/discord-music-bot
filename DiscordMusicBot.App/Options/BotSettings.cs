namespace DiscordMusicBot.App.Options;

public sealed class BotSettings
{
    public const string SectionName = "BotSettings";

    public required string BotToken { get; init; } = null!;
    public required string AppId { get; init; } = null!;
    public required string PublicKey { get; init; } = null!;
}
