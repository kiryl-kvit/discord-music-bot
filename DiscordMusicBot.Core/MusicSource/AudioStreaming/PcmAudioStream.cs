namespace DiscordMusicBot.Core.MusicSource.AudioStreaming;

public sealed class PcmAudioStream(
    Stream stream,
    string title,
    Func<ValueTask>? onDispose = null) : IAsyncDisposable
{
    public Stream Stream { get; } = stream;

    public string Title { get; } = title;

    public async ValueTask DisposeAsync()
    {
        await Stream.DisposeAsync();

        if (onDispose is not null)
        {
            await onDispose.Invoke();
        }
    }
}
