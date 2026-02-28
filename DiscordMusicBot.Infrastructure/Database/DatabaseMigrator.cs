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

        if (!result.Scripts.Any())
        {
            logger.LogInformation("Database is up to date. No migrations applied");
            return;
        }

        foreach (var script in result.Scripts)
        {
            logger.LogInformation("Applied migration: {ScriptName}", script.Name);
        }

        logger.LogInformation("Database migrations completed successfully. {Count} migration(s) applied",
            result.Scripts.Count());
    }
}
