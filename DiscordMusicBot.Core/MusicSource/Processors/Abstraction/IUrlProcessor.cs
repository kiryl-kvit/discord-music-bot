namespace DiscordMusicBot.Core.MusicSource.Processors.Abstraction;

public interface IUrlProcessor
{
    Task<Result<MusicSourceResult>> GetMusicItemsAsync(string url,
        CancellationToken cancellationToken = default);
}