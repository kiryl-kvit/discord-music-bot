namespace DiscordMusicBot.App.Configuration;

public static class EnvKeyMapping
{
    public static IReadOnlyDictionary<string, string> Mappings { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BOT_TOKEN"] = "BotSettings:BotToken",
            ["APP_ID"] = "BotSettings:AppId",
            ["PUBLIC_KEY"] = "BotSettings:PublicKey",
            ["PLAYLIST_LIMIT"] = "MusicSources:PlaylistLimit",
            ["VOLUME"] = "MusicSources:Volume",
        };
}
