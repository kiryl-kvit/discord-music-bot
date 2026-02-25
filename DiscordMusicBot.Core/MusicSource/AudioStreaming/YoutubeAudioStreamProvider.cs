using DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;
using Microsoft.Extensions.Logging;
using YoutubeExplode;
using YoutubeExplode.Videos;

namespace DiscordMusicBot.Core.MusicSource.AudioStreaming;

public sealed class YoutubeAudioStreamProvider(
    YoutubeClient youtubeClient,
    FfmpegAudioPipeline ffmpeg,
    ILogger<YoutubeAudioStreamProvider> logger) : IAudioStreamProvider
{
    public async Task<Result<ResolvedStream>> ResolveStreamAsync(string url,
        CancellationToken cancellationToken = default)
    {
        var videoId = VideoId.TryParse(url);
        if (videoId is null)
        {
            return Result<ResolvedStream>.Failure("Invalid YouTube URL.");
        }

        try
        {
            var manifest = await youtubeClient.Videos.Streams.GetManifestAsync(videoId.Value, cancellationToken);

            const long targetBitrateKbps = 64;

            var streamInfo = manifest
                .GetAudioOnlyStreams()
                .OrderBy(s => Math.Abs(s.Bitrate.KiloBitsPerSecond - targetBitrateKbps))
                .FirstOrDefault();

            if (streamInfo is null)
            {
                return Result<ResolvedStream>.Failure("No audio streams available for this video.");
            }

            logger.LogInformation(
                "Resolved audio stream for '{VideoId}': {Codec} @ {Bitrate}kbps, container: {Container}",
                videoId, streamInfo.AudioCodec, streamInfo.Bitrate.KiloBitsPerSecond, streamInfo.Container);

            return Result<ResolvedStream>.Success(new ResolvedStream(streamInfo.Url, url));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to resolve audio stream for YouTube video {VideoId}.", videoId);
            return Result<ResolvedStream>.Failure("Unable to fetch audio stream for this video.");
        }
    }

    public Task<Result<PcmAudioStream>> GetAudioStreamAsync(ResolvedStream resolved,
        TimeSpan startFrom = default, CancellationToken cancellationToken = default)
        => ffmpeg.GetAudioStreamAsync(resolved, startFrom, cancellationToken);
}
