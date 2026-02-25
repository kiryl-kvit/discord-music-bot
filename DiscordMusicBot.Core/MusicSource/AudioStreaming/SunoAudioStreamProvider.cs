using DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;
using DiscordMusicBot.Core.MusicSource.Suno;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.Core.MusicSource.AudioStreaming;

public sealed class SunoAudioStreamProvider(
    FfmpegAudioPipeline ffmpeg,
    ILogger<SunoAudioStreamProvider> logger) : IAudioStreamProvider
{
    public Task<Result<ResolvedStream>> ResolveStreamAsync(string url,
        CancellationToken cancellationToken = default)
    {
        var songId = SunoTrack.ExtractSongId(url);
        if (songId is null)
        {
            return Task.FromResult(Result<ResolvedStream>.Failure("Invalid Suno song URL."));
        }

        var cdnUrl = SunoTrack.GetCdnUrl(songId);

        logger.LogInformation("Resolved Suno CDN stream for song {SongId}: {CdnUrl}", songId, cdnUrl);

        return Task.FromResult(Result<ResolvedStream>.Success(new ResolvedStream(cdnUrl, url)));
    }

    public Task<Result<PcmAudioStream>> GetAudioStreamAsync(ResolvedStream resolved,
        TimeSpan startFrom = default, CancellationToken cancellationToken = default)
        => ffmpeg.GetAudioStreamAsync(resolved, startFrom, cancellationToken);
}
