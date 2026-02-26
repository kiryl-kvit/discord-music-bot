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

    public async Task<Result<IReadOnlyCollection<MusicSource>>> GetMusicItemsAsync(string url,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedSources.TryGetSourceKey(url, out var key) ||
            !string.Equals(key, SupportedSources.SunoKey, StringComparison.OrdinalIgnoreCase))
        {
            return Result<IReadOnlyCollection<MusicSource>>.Failure("Unsupported Suno URL.");
        }

        if (!TryParseSunoUrl(url, out var resourceType, out var resourceId))
        {
            return Result<IReadOnlyCollection<MusicSource>>.Failure(
                "Invalid Suno URL. Expected a song or playlist link.");
        }

        return resourceType switch
        {
            SunoResourceType.Song => await ProcessSongAsync(resourceId, cancellationToken),
            SunoResourceType.Playlist => await ProcessPlaylistAsync(resourceId, cancellationToken),
            _ => Result<IReadOnlyCollection<MusicSource>>.Failure("Unsupported Suno resource type."),
        };
    }

    private async Task<Result<IReadOnlyCollection<MusicSource>>> ProcessSongAsync(string songId,
        CancellationToken cancellationToken)
    {
        try
        {
            var track = await metadataClient.GetSongAsync(songId, cancellationToken);

            if (track is null)
            {
                return Result<IReadOnlyCollection<MusicSource>>.Failure(
                    $"Could not fetch metadata for Suno song '{songId}'.");
            }

            var musicSource = ToMusicSource(track);
            return Result<IReadOnlyCollection<MusicSource>>.Success([musicSource]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to process Suno song {SongId}.", songId);
            return Result<IReadOnlyCollection<MusicSource>>.Failure("Unable to fetch Suno song metadata.");
        }
    }

    private async Task<Result<IReadOnlyCollection<MusicSource>>> ProcessPlaylistAsync(string playlistId,
        CancellationToken cancellationToken)
    {
        try
        {
            var tracks = await metadataClient.GetPlaylistTracksAsync(playlistId, cancellationToken);

            if (tracks.Count == 0)
            {
                return Result<IReadOnlyCollection<MusicSource>>.Failure(
                    "Suno playlist is empty or inaccessible.");
            }

            var musicSources = tracks.Select(ToMusicSource).ToList();

            logger.LogInformation("Resolved {Count} tracks from Suno playlist {PlaylistId}.",
                musicSources.Count, playlistId);

            return Result<IReadOnlyCollection<MusicSource>>.Success(musicSources);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to process Suno playlist {PlaylistId}.", playlistId);
            return Result<IReadOnlyCollection<MusicSource>>.Failure("Unable to fetch Suno playlist metadata.");
        }
    }

    private static MusicSource ToMusicSource(SunoTrack track)
    {
        return new MusicSource(track.Title, track.SongUrl, track.Artist, Duration: null);
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