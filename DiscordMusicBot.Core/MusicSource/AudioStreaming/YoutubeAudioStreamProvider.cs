using System.IO.Pipelines;
using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;
using FFMpegCore;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace DiscordMusicBot.Core.MusicSource.AudioStreaming;

public sealed class YoutubeAudioStreamProvider(
    YoutubeClient youtubeClient,
    ILogger<YoutubeAudioStreamProvider> logger) : IAudioStreamProvider
{
    public async Task<Result<PcmAudioStream>> GetAudioStreamAsync(string url,
        TimeSpan startFrom = default, CancellationToken cancellationToken = default)
    {
        var videoId = VideoId.TryParse(url);
        if (videoId is null)
        {
            return Result<PcmAudioStream>.Failure("Invalid YouTube URL.");
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
                return Result<PcmAudioStream>.Failure("No audio streams available for this video.");
            }

            logger.LogInformation(
                "Resolved audio stream for '{VideoId}': {Codec} @ {Bitrate}kbps, container: {Container}",
                videoId, streamInfo.AudioCodec, streamInfo.Bitrate.KiloBitsPerSecond, streamInfo.Container);

            var audioStreamUrl = streamInfo.Url;
            var pipe = new Pipe();

            var ffmpegTask = RunFfmpegAsync(audioStreamUrl, startFrom, pipe.Writer, cancellationToken);

            var pcmAudioStream = new PcmAudioStream(
                pipe.Reader.AsStream(),
                url,
                async () =>
                {
                    // Complete the reader so the FFmpeg pipe-writer side gets a broken pipe
                    // and terminates. Do NOT await ffmpegTask here — FFmpeg may take time to
                    // die after the pipe breaks, and blocking disposal would stall the
                    // playback advancement loop on skip.
                    await pipe.Reader.CompleteAsync();
                    _ = ffmpegTask.ContinueWith(_ => { }, TaskScheduler.Default);
                });

            return Result<PcmAudioStream>.Success(pcmAudioStream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to get audio stream for YouTube video {VideoId}.", videoId);
            return Result<PcmAudioStream>.Failure("Unable to fetch audio stream for this video.");
        }
    }

    private async Task RunFfmpegAsync(string inputUrl, TimeSpan startFrom, PipeWriter pipeWriter,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var writerStream = pipeWriter.AsStream();
            var pipeSink = new StreamPipeSink(writerStream);

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
                .OutputToPipe(pipeSink, options => options
                    .WithCustomArgument("-vn")
                    .WithAudioSamplingRate(48000)
                    .ForceFormat("s16le")
                    .WithCustomArgument("-ac 2"))
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
