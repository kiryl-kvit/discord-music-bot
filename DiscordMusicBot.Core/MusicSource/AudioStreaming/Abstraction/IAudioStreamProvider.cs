namespace DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;

public interface IAudioStreamProvider
{
    Task<Result<ResolvedStream>> ResolveStreamAsync(string url,
        CancellationToken cancellationToken = default);

    Task<Result<PcmAudioStream>> GetAudioStreamAsync(ResolvedStream resolved, TimeSpan startFrom = default,
        CancellationToken cancellationToken = default);
}
