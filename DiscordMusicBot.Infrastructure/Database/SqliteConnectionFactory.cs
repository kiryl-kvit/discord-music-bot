using DiscordMusicBot.Infrastructure.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace DiscordMusicBot.Infrastructure.Database;

public sealed class SqliteConnectionFactory(IOptions<DatabaseOptions> options)
{
    public string ConnectionString { get; } = BuildConnectionString(options.Value.Path);

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(ConnectionString);
    }

    private static string BuildConnectionString(string path)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Cache = SqliteCacheMode.Shared,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }
}
