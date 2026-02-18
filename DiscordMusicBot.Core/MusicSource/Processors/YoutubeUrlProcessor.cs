using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;

namespace DiscordMusicBot.Core.MusicSource.Processors;

public sealed class YoutubeUrlProcessor : IUrlProcessor
{
    public Task<Result<IReadOnlyCollection<MusicSource>>> GetMusicItemsAsync(string url,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedSources.TryGetSourceKey(url, out var key) ||
            !string.Equals(key, SupportedSources.YoutubeKey, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(
                Result<IReadOnlyCollection<MusicSource>>.Failure("Unsupported YouTube URL."));
        }
        
        var isPlaylist = url.Contains("=list");

        return Task.FromResult(Result<IReadOnlyCollection<MusicSource>>.Success([]));
    }
}