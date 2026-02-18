using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusicBot.Core.MusicSource.Processors;

public sealed class UrlProcessorFactory(IServiceProvider serviceProvider) : IUrlProcessorFactory
{
    public IUrlProcessor GetProcessor(string url)
    {
        if (!SupportedSources.TryGetSourceKey(url, out var key))
        {
            throw new InvalidOperationException("No metadata processor registered for the provided URL.");
        }

        return serviceProvider.GetRequiredKeyedService<IUrlProcessor>(key);
    }
}