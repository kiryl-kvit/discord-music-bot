using DiscordMusicBot.Domain.History;
using DiscordMusicBot.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusicBot.App.Services;

public sealed class DataCleanupService(
    IHistoryRepository historyRepository,
    IOptions<DataCleanupOptions> options,
    ILogger<DataCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;

        logger.LogInformation(
            "Data cleanup service started (retention: {RetentionDays} days, interval: {IntervalHours} hours)",
            config.RetentionDays, config.IntervalHours);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(config.IntervalHours));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupAsync(config.RetentionDays, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(int retentionDays, CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = DateTime.UtcNow - TimeSpan.FromDays(retentionDays);
            var deleted = await historyRepository.DeleteOlderThanAsync(cutoff, cancellationToken);

            if (deleted > 0)
            {
                logger.LogInformation("Data cleanup removed {Count} history records older than {Cutoff:u}", deleted, cutoff);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Data cleanup failed");
        }
    }
}
