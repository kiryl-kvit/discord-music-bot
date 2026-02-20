using DiscordMusicBot.DataAccess.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusicBot.DataAccess;

public sealed class DbInitializerHostedService(
    IServiceProvider serviceProvider,
    IOptions<SqliteDatabaseOptions> options,
    ILogger<DbInitializerHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var dbFilePath = options.Value.DbFilePath;
        var directory = Path.GetDirectoryName(dbFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MusicBotDbContext>();

        logger.LogInformation("Ensuring sqlite database exists at {DbFilePath}", dbFilePath);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
