using Discord;
using Discord.WebSocket;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DiscordMusicBot.App;
using DiscordMusicBot.App.Options;
using DiscordMusicBot.App.Services;
using Microsoft.Extensions.Options;

var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
Env.Load(envPath);

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((_, config) =>
    {
        var basePath = AppContext.BaseDirectory;
        config.SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        var envValues = new Dictionary<string, string?>
        {
            ["BotSettings:BotToken"] = Environment.GetEnvironmentVariable("BOT_TOKEN"),
            ["BotSettings:AppId"] = Environment.GetEnvironmentVariable("APP_ID"),
            ["BotSettings:PublicKey"] = Environment.GetEnvironmentVariable("PUBLIC_KEY"),
            ["MusicSources:PlaylistLimit"] = Environment.GetEnvironmentVariable("PLAYLIST_LIMIT"),
            ["MusicSources:Volume"] = Environment.GetEnvironmentVariable("VOLUME"),
        };
        config.AddInMemoryCollection(envValues.Where(kv => kv.Value is not null));
    })
    .ConfigureServices((ctx, services) => { ServicesConfiguration.ConfigureServices(services, ctx.Configuration); })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build();

var voiceConnectionService = host.Services.GetRequiredService<VoiceConnectionService>();
var queuePlaybackService = host.Services.GetRequiredService<QueuePlaybackService>();

var discordClient = host.Services.GetRequiredService<DiscordSocketClient>();
discordClient.UserVoiceStateUpdated += voiceConnectionService.HandleVoiceStateUpdated;

voiceConnectionService.Connected += queuePlaybackService.OnVoiceConnected;
voiceConnectionService.Disconnected += queuePlaybackService.OnVoiceDisconnected;

var interactionHandler = host.Services.GetRequiredService<InteractionHandler>();
await interactionHandler.InitializeAsync();

var botSettings = host.Services.GetRequiredService<IOptions<BotSettings>>().Value;
await discordClient.LoginAsync(TokenType.Bot, botSettings.BotToken);
await discordClient.StartAsync();

await host.RunAsync();