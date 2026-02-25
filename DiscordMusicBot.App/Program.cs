using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DiscordMusicBot.App;
using DiscordMusicBot.App.Configuration;
using DiscordMusicBot.App.Options;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Infrastructure.Database;
using Microsoft.Extensions.Options;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((_, config) =>
    {
        var basePath = AppContext.BaseDirectory;
        config.SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvFile(".env", EnvKeyMapping.Mappings);
    })
    .ConfigureServices((ctx, services) => { ServicesConfiguration.ConfigureServices(services, ctx.Configuration); })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build();

var migrator = host.Services.GetRequiredService<DatabaseMigrator>();
migrator.Migrate();

var voiceConnectionService = host.Services.GetRequiredService<VoiceConnectionService>();
var queuePlaybackService = host.Services.GetRequiredService<QueuePlaybackService>();

var discordClient = host.Services.GetRequiredService<DiscordSocketClient>();
discordClient.UserVoiceStateUpdated += voiceConnectionService.HandleVoiceStateUpdated;

voiceConnectionService.Connected += queuePlaybackService.OnVoiceConnected;
voiceConnectionService.Disconnected += queuePlaybackService.OnVoiceDisconnected;

var restoreComplete = false;
discordClient.Ready += async () =>
{
    if (restoreComplete)
    {
        return;
    }

    restoreComplete = true;
    await queuePlaybackService.RestoreAsync();
};

var interactionHandler = host.Services.GetRequiredService<InteractionHandler>();
await interactionHandler.InitializeAsync();

var botSettings = host.Services.GetRequiredService<IOptions<BotSettings>>().Value;
await discordClient.LoginAsync(TokenType.Bot, botSettings.BotToken);
await discordClient.StartAsync();

await host.RunAsync();

await voiceConnectionService.DisconnectAllAsync();
await discordClient.StopAsync();