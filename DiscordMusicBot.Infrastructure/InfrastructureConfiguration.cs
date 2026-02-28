using DiscordMusicBot.Domain.Favorites;
using DiscordMusicBot.Domain.History;
using DiscordMusicBot.Domain.Playback;
using DiscordMusicBot.Domain.Playlists;
using DiscordMusicBot.Domain.PlayQueue;
using DiscordMusicBot.Domain.Settings;
using DiscordMusicBot.Infrastructure.Database;
using DiscordMusicBot.Infrastructure.Options;
using DiscordMusicBot.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusicBot.Infrastructure;

public static class InfrastructureConfiguration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DapperConfiguration.Configure();

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName));

        services.AddOptions<DataCleanupOptions>()
            .Bind(configuration.GetSection(DataCleanupOptions.SectionName))
            .Validate(o => o.RetentionDays >= 1, "DataCleanup:RetentionDays must be at least 1.")
            .Validate(o => o.IntervalHours > 0, "DataCleanup:IntervalHours must be greater than 0.")
            .ValidateOnStart();

        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<DatabaseMigrator>();
        services.AddSingleton<IPlayQueueRepository, PlayQueueRepository>();
        services.AddSingleton<IGuildPlaybackStateRepository, GuildPlaybackStateRepository>();
        services.AddSingleton<IFavoriteRepository, FavoriteRepository>();
        services.AddSingleton<IPlaylistRepository, PlaylistRepository>();
        services.AddSingleton<IHistoryRepository, HistoryRepository>();
        services.AddSingleton<IGuildSettingsRepository, GuildSettingsRepository>();

        return services;
    }
}
