using DiscordMusicBot.Core.MusicSource.AudioStreaming;

namespace DiscordMusicBot.App.Services.Models;

public sealed class PrefetchedTrack : IAsyncDisposable
{
    public required long ItemId { get; init; }
    public required PcmAudioStream Stream { get; init; }

    public async ValueTask DisposeAsync()
    {
        await Stream.DisposeAsync();
    }
}
