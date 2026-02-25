using System.Globalization;
using System.IO.Pipelines;
using DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;
using DiscordMusicBot.Core.MusicSource.Options;
using FFMpegCore;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos;

namespace DiscordMusicBot.Core.MusicSource.AudioStreaming;

public sealed class YoutubeAudioStreamProvider(
    YoutubeClient youtubeClient,
    IOptionsMonitor<MusicSourcesOptions> musicSourcesOptions,
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
    {
        try
        {
            var pipe = new Pipe();

            var ffmpegTask = RunFfmpegAsync(resolved.StreamUrl, startFrom, pipe.Writer, cancellationToken);

            var pcmAudioStream = new PcmAudioStream(
                pipe.Reader.AsStream(),
                resolved.SourceUrl,
                async () =>
                {
                    // Complete the reader so the FFmpeg pipe-writer side gets a broken pipe
                    // and terminates. Do NOT await ffmpegTask here — FFmpeg may take time to
                    // die after the pipe breaks, and blocking disposal would stall the
                    // playback advancement loop on skip.
                    await pipe.Reader.CompleteAsync();
                    _ = ffmpegTask.ContinueWith(_ => { }, TaskScheduler.Default);
                });

            return Task.FromResult(Result<PcmAudioStream>.Success(pcmAudioStream));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to launch FFmpeg for resolved stream '{SourceUrl}'.", resolved.SourceUrl);
            return Task.FromResult(Result<PcmAudioStream>.Failure("Unable to launch audio stream for this video."));
        }
    }

    private async Task RunFfmpegAsync(string inputUrl, TimeSpan startFrom, PipeWriter pipeWriter,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var writerStream = pipeWriter.AsStream();
            var pipeSink = new StreamPipeSink(writerStream);

            var volume = musicSourcesOptions.CurrentValue.Volume;

            await FFMpegArguments
                .FromUrlInput(new Uri(inputUrl), options =>
                {
                    if (startFrom > TimeSpan.Zero)
                    {
                        options.WithCustomArgument($"-ss {startFrom:hh\\:mm\\:ss\\.fff}");
                    }

                    options
                        .WithCustomArgument("-reconnect 1")
                        .WithCustomArgument("-reconnect_streamed 1")
                        .WithCustomArgument("-reconnect_delay_max 5");
                })
                .OutputToPipe(pipeSink, options =>
                {
                    options
                        .WithCustomArgument("-vn")
                        .WithAudioSamplingRate(48000)
                        .ForceFormat("s16le")
                        .WithCustomArgument("-ac 2");

                    if (Math.Abs(volume - 1.0) > 0.001)
                    {
                        options.WithCustomArgument(
                            $"-af volume={volume.ToString("F2", CultureInfo.InvariantCulture)}");
                    }
                })
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously();
        }
        catch (OperationCanceledException)
        {
            //
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FFmpeg process failed for input URL.");
        }
        finally
        {
            await pipeWriter.CompleteAsync();
        }
    }
}
