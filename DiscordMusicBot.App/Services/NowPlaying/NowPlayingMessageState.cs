using Discord;

namespace DiscordMusicBot.App.Services.NowPlaying;

internal sealed class NowPlayingMessageState
{
    public ulong ChannelId { get; init; }
    public ulong MessageId { get; init; }
    public required IMessageChannel Channel { get; init; }
    public long LastEditUtcTicks;
    public CancellationTokenSource? TimerCts;

    /// <summary>
    /// Serializes all modify-message calls for this guild so at most one PATCH is in flight.
    /// </summary>
    public readonly SemaphoreSlim EditSemaphore = new(1, 1);

    /// <summary>
    /// Set when an update was skipped due to debounce. The next timer tick will
    /// force an update to ensure the embed eventually reflects the latest state.
    /// </summary>
    public volatile bool PendingUpdate;

    /// <summary>
    /// Set when the next track is loading. The loading embed is deferred until
    /// the next timer tick to avoid rapid-fire PATCHes during track transitions.
    /// Cleared when <c>TrackStarted</c> fires.
    /// </summary>
    public volatile bool IsLoading;

    /// <summary>
    /// Fingerprint of the last successfully sent embed content, used to skip no-op PATCHes.
    /// </summary>
    public string? LastContentHash;
}