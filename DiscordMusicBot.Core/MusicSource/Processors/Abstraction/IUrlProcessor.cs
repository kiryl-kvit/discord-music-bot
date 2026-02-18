namespace DiscordMusicBot.Core.MusicSource.Processors.Abstraction;

public interface IUrlProcessor
{
    Task<Result<IReadOnlyCollection<MusicSource>>> GetMusicItemsAsync(string url,
        CancellationToken cancellationToken = default);
}