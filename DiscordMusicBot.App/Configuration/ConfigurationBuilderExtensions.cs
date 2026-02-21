using Microsoft.Extensions.Configuration;

namespace DiscordMusicBot.App.Configuration;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddEnvFile(
        this IConfigurationBuilder builder,
        string fileName,
        IReadOnlyDictionary<string, string> keyMapping)
    {
        var filePath = ResolveEnvFilePath(fileName);
        return builder.Add(new EnvFileConfigurationSource(filePath, keyMapping));
    }

    private static string ResolveEnvFilePath(string fileName)
    {
        var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        if (File.Exists(cwdPath))
        {
            return Path.GetFullPath(cwdPath);
        }

        var baseDirPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(baseDirPath))
        {
            return Path.GetFullPath(baseDirPath);
        }

        var directory = Directory.GetParent(AppContext.BaseDirectory);
        for (var i = 0; i < 5 && directory is not null; i++)
        {
            var candidatePath = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidatePath))
            {
                return Path.GetFullPath(candidatePath);
            }

            directory = directory.Parent;
        }

        // Default to CWD so the watcher can pick it up if created later.
        return Path.GetFullPath(cwdPath);
    }
}
