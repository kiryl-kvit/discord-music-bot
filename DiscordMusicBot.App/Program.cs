using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DiscordMusicBot.App;
using DiscordMusicBot.App.Configuration;

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

await host.RunAsync();
