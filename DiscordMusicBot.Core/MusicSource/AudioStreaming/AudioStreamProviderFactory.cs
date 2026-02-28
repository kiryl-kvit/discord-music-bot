using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusicBot.Core.MusicSource.AudioStreaming;

public sealed class AudioStreamProviderFactory(IServiceProvider serviceProvider) : IAudioStreamProviderFactory
{
    public IAudioStreamProvider GetProvider(string url)
    {
        if (!SupportedSources.TryGetSourceType(url, out var sourceType))
        {
            throw new InvalidOperationException("No audio stream provider registered for the provided URL.");
        }

        return serviceProvider.GetRequiredKeyedService<IAudioStreamProvider>(sourceType);
    }
}
