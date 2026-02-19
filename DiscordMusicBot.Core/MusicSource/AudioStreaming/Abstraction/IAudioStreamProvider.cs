namespace DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;

public interface IAudioStreamProvider
{
    Task<Result<PcmAudioStream>> GetAudioStreamAsync(string url, CancellationToken cancellationToken = default);
}
