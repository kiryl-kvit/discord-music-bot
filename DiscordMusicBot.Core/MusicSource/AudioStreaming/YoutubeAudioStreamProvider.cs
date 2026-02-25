using DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;
using Microsoft.Extensions.Logging;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
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
        catch (VideoRequiresPurchaseException ex)
        {
            logger.LogWarning(ex, "YouTube video {VideoId} requires purchase.", videoId);
            return Result<ResolvedStream>.Failure("This video requires purchase and cannot be played.");
        }
        catch (VideoUnavailableException ex)
        {
            logger.LogWarning(ex, "YouTube video {VideoId} is unavailable.", videoId);
            return Result<ResolvedStream>.Failure(
                "This video is unavailable. It may have been removed or set to private.");
        }
        catch (VideoUnplayableException ex)
        {
            logger.LogWarning(ex, "YouTube video {VideoId} is unplayable.", videoId);
            return Result<ResolvedStream>.Failure(
                "This video is not available for playback. It may be region-restricted or require authentication.");
        }
        catch (RequestLimitExceededException ex)
        {
            logger.LogWarning(ex, "YouTube rate limit hit while resolving video {VideoId}.", videoId);
            return Result<ResolvedStream>.Failure(
                "YouTube is rate-limiting requests. Please try again later.");
        }
        catch (YoutubeExplodeException ex)
        {
            logger.LogWarning(ex, "Failed to resolve audio stream for YouTube video {VideoId}.", videoId);
            return Result<ResolvedStream>.Failure(
                "This video cannot be played. It may be region-restricted, require authentication, or be otherwise unavailable.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Unexpected error resolving audio stream for YouTube video {VideoId}.", videoId);
            return Result<ResolvedStream>.Failure("An unexpected error occurred while fetching the audio stream.");
        }
    }

    public Task<Result<PcmAudioStream>> GetAudioStreamAsync(ResolvedStream resolved,
        TimeSpan startFrom = default, CancellationToken cancellationToken = default)
        => ffmpeg.GetAudioStreamAsync(resolved, startFrom, cancellationToken);
}
