using System.Text.RegularExpressions;
using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using DiscordMusicBot.Core.MusicSource.Suno;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.Core.MusicSource.Processors;

public sealed partial class SunoUrlProcessor(
    SunoMetadataClient metadataClient,
    ILogger<SunoUrlProcessor> logger)
    : IUrlProcessor
{
    [GeneratedRegex(@"suno\.com/(?<type>song|playlist)/(?<id>[0-9a-fA-F-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SunoUrlPattern();

    public async Task<Result<MusicSourceResult>> GetMusicItemsAsync(string url,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedSources.TryGetSourceType(url, out var sourceType) ||
            sourceType != SourceType.Suno)
        {
            return Result<MusicSourceResult>.Failure("Unsupported Suno URL.");
        }

        if (!TryParseSunoUrl(url, out var resourceType, out var resourceId))
        {
            return Result<MusicSourceResult>.Failure(
                "Invalid Suno URL. Expected a song or playlist link.");
        }

        return resourceType switch
        {
            SunoResourceType.Song => await ProcessSongAsync(resourceId, cancellationToken),
            SunoResourceType.Playlist => await ProcessPlaylistAsync(resourceId, cancellationToken),
            _ => Result<MusicSourceResult>.Failure("Unsupported Suno resource type."),
        };
    }

    private async Task<Result<MusicSourceResult>> ProcessSongAsync(string songId,
        CancellationToken cancellationToken)
    {
        try
        {
            var track = await metadataClient.GetSongAsync(songId, cancellationToken);

            if (track is null)
            {
                return Result<MusicSourceResult>.Failure(
                    $"Could not fetch metadata for Suno song '{songId}'.");
            }

            var musicSource = ToMusicSource(track);
            return Result<MusicSourceResult>.Success(new MusicSourceResult([musicSource]));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to process Suno song {SongId}.", songId);
            return Result<MusicSourceResult>.Failure("Unable to fetch Suno song metadata.");
        }
    }

    private async Task<Result<MusicSourceResult>> ProcessPlaylistAsync(string playlistId,
        CancellationToken cancellationToken)
    {
        try
        {
            var (playlistName, tracks) = await metadataClient.GetPlaylistAsync(playlistId, cancellationToken);

            if (tracks.Count == 0)
            {
                return Result<MusicSourceResult>.Failure(
                    "Suno playlist is empty or inaccessible.");
            }

            var musicSources = tracks.Select(ToMusicSource).ToList();

            logger.LogInformation("Resolved {Count} tracks from Suno playlist {PlaylistId}.",
                musicSources.Count, playlistId);

            return Result<MusicSourceResult>.Success(new MusicSourceResult(musicSources, playlistName));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to process Suno playlist {PlaylistId}.", playlistId);
            return Result<MusicSourceResult>.Failure("Unable to fetch Suno playlist metadata.");
        }
    }

    private static MusicSource ToMusicSource(SunoTrack track)
    {
        return new MusicSource(SourceType.Suno, track.Title, track.SongUrl, track.Artist, track.Duration, track.ImageUrl);
    }

    private static bool TryParseSunoUrl(string url, out SunoResourceType resourceType, out string resourceId)
    {
        resourceType = default;
        resourceId = string.Empty;

        var match = SunoUrlPattern().Match(url);
        if (!match.Success)
        {
            return false;
        }

        var type = match.Groups["type"].Value;
        resourceId = match.Groups["id"].Value;

        resourceType = type.ToLowerInvariant() switch
        {
            "song" => SunoResourceType.Song,
            "playlist" => SunoResourceType.Playlist,
            _ => default,
        };

        return resourceType is not default(SunoResourceType);
    }
}