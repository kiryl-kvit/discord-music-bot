namespace DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;

public interface IAudioStreamProviderFactory
{
    IAudioStreamProvider GetProvider(string url);
}
