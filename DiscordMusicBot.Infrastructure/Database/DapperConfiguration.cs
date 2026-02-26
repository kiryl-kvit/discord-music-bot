using Dapper;

namespace DiscordMusicBot.Infrastructure.Database;

public static class DapperConfiguration
{
    public static void Configure()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}
