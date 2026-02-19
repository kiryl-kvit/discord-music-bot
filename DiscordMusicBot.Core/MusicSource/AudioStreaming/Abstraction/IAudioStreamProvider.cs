namespace DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;

public interface IAudioStreamProvider
{
    Task<Result<PcmAudioStream>> GetAudioStreamAsync(string url, TimeSpan startFrom = default,
        CancellationToken cancellationToken = default);
}
