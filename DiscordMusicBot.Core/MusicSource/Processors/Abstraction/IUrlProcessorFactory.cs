namespace DiscordMusicBot.Core.MusicSource.Processors.Abstraction;

public interface IUrlProcessorFactory
{
    IUrlProcessor GetProcessor(string url);
}
