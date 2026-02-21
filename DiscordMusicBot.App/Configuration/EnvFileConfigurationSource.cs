using Microsoft.Extensions.Configuration;

namespace DiscordMusicBot.App.Configuration;

public sealed class EnvFileConfigurationSource(
    string filePath,
    IReadOnlyDictionary<string, string> keyMapping) : IConfigurationSource
{
    public string FilePath { get; } = filePath;
    public IReadOnlyDictionary<string, string> KeyMapping { get; } = keyMapping;

    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new EnvFileConfigurationProvider(this);
}
