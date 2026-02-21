namespace DiscordMusicBot.Core.MusicSource.AudioStreaming;

/// <summary>
/// Holds a pre-resolved audio stream URL (e.g. YouTube CDN link) so that
/// the expensive FFmpeg launch can be deferred until actual playback time.
/// </summary>
public sealed record ResolvedStream(string StreamUrl, string SourceUrl);
