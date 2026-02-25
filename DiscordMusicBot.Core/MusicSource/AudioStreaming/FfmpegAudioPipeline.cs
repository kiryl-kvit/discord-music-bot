using System.Globalization;
using System.IO.Pipelines;
using DiscordMusicBot.Core.MusicSource.Options;
using FFMpegCore;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusicBot.Core.MusicSource.AudioStreaming;

public sealed class FfmpegAudioPipeline(
    IOptionsMonitor<MusicSourcesOptions> musicSourcesOptions,
    ILogger<FfmpegAudioPipeline> logger)
{
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
            return Task.FromResult(Result<PcmAudioStream>.Failure("Unable to launch audio stream."));
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
                        .WithAudioSamplingRate()
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
