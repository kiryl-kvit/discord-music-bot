using DiscordMusicBot.Domain.Playback;
using DiscordMusicBot.Domain.PlayQueue;
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

        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<DatabaseMigrator>();
        services.AddSingleton<IPlayQueueRepository, PlayQueueRepository>();
        services.AddSingleton<IGuildPlaybackStateRepository, GuildPlaybackStateRepository>();

        return services;
    }
}
