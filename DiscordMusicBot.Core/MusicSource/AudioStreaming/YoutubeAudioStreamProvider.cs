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
        CancellationToken cancellationToken = default)
    {
        var videoId = VideoId.TryParse(url);
        if (videoId is null)
        {
            return Result<PcmAudioStream>.Failure("Invalid YouTube URL.");
        }

        try
        {
            var manifest = await youtubeClient.Videos.Streams.GetManifestAsync(videoId.Value, cancellationToken);

            var streamInfo = manifest
                .GetAudioOnlyStreams()
                .OrderByDescending(s => s.Bitrate)
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

            var ffmpegTask = RunFfmpegAsync(audioStreamUrl, pipe.Writer, cancellationToken);

            var pcmAudioStream = new PcmAudioStream(
                pipe.Reader.AsStream(),
                url,
                async () =>
                {
                    await pipe.Reader.CompleteAsync();
                    await ffmpegTask;
                });

            return Result<PcmAudioStream>.Success(pcmAudioStream);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get audio stream for YouTube video {VideoId}.", videoId);
            return Result<PcmAudioStream>.Failure("Unable to fetch audio stream for this video.");
        }
    }

    private async Task RunFfmpegAsync(string inputUrl, PipeWriter pipeWriter,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var writerStream = pipeWriter.AsStream();
            var pipeSink = new StreamPipeSink(writerStream);

            await FFMpegArguments
                .FromUrlInput(new Uri(inputUrl), options => options
                    .WithCustomArgument("-reconnect 1")
                    .WithCustomArgument("-reconnect_streamed 1")
                    .WithCustomArgument("-reconnect_delay_max 5"))
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
