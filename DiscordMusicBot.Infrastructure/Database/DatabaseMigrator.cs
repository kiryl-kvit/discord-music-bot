using DbUp;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.Infrastructure.Database;

public sealed class DatabaseMigrator(
    SqliteConnectionFactory connectionFactory,
    ILogger<DatabaseMigrator> logger)
{
    public void Migrate()
    {
        var connectionString = connectionFactory.ConnectionString;

        logger.LogInformation("Running database migrations...");

        var upgrader = DeployChanges.To
            .SqliteDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly)
            .LogToNowhere()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            logger.LogError(result.Error, "Database migration failed");
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }

        var scriptsApplied = result.Scripts.Count();
        logger.LogInformation("Database migrations completed successfully. {Count} script(s) applied",
            scriptsApplied);
    }
}
