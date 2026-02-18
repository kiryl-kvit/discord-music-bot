using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordMusicBot.App.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App;

public static class ServicesConfiguration
{
    public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BotSettings>(configuration.GetSection(BotSettings.SectionName));

        var socketConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged,
            LogLevel = LogSeverity.Info,
        };

        services.AddSingleton(socketConfig);
        services.AddSingleton<DiscordSocketClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DiscordSocketClient>>();
            var client = new DiscordSocketClient(socketConfig);

            client.Log += Logging.CreateLogHandler(logger);

            return client;
        });

        services.AddSingleton<InteractionService>(sp =>
        {
            var client = sp.GetRequiredService<DiscordSocketClient>();
            var logger = sp.GetRequiredService<ILogger<InteractionService>>();
            
            var interactionService = new InteractionService(client);

            interactionService.Log += Logging.CreateLogHandler(logger);

            return interactionService;
        });

        services.AddSingleton<InteractionHandler>();

        return services;
    }
}