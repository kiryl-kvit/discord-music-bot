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
            ["SPOTIFY_CLIENT_ID"] = "Spotify:ClientId",
            ["SPOTIFY_CLIENT_SECRET"] = "Spotify:ClientSecret",
            ["SUNO_ENABLED"] = "Suno:Enabled",
            ["DATABASE_PATH"] = "Database:Path",
            ["FAVORITES_LIMIT"] = "Favorites:Limit",
            ["CLEANUP_RETENTION_DAYS"] = "DataCleanup:RetentionDays",
            ["CLEANUP_INTERVAL_HOURS"] = "DataCleanup:IntervalHours",
        };
}
