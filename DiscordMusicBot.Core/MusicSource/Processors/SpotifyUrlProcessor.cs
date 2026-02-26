using System.Text.RegularExpressions;
using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.Options;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using DiscordMusicBot.Core.MusicSource.Spotify;
using DiscordMusicBot.Core.MusicSource.Youtube;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;
using YoutubeExplode;

namespace DiscordMusicBot.Core.MusicSource.Processors;

public sealed partial class SpotifyUrlProcessor(
    SpotifyClientProvider spotifyClientProvider,
    YoutubeClient youtubeClient,
    IOptionsMonitor<MusicSourcesOptions> options,
    ILogger<SpotifyUrlProcessor> logger)
    : IUrlProcessor
{
    private const int MaxYoutubeSearchConcurrency = 4;

    [GeneratedRegex(@"open\.spotify\.com/(?<type>track|playlist|album)/(?<id>[a-zA-Z0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SpotifyUrlPattern();

    public async Task<Result<MusicSourceResult>> GetMusicItemsAsync(string url,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedSources.TryGetSourceKey(url, out var key) ||
            !string.Equals(key, SupportedSources.SpotifyKey, StringComparison.OrdinalIgnoreCase))
        {
            return Result<MusicSourceResult>.Failure("Unsupported Spotify URL.");
        }

        if (!TryParseSpotifyUrl(url, out var resourceType, out var resourceId))
        {
            return Result<MusicSourceResult>.Failure(
                "Invalid Spotify URL. Expected a track, playlist, or album link.");
        }

        return resourceType switch
        {
            SpotifyResourceType.Track => await ProcessTrackAsync(resourceId, cancellationToken),
            SpotifyResourceType.Playlist => await ProcessPlaylistAsync(resourceId, cancellationToken),
            SpotifyResourceType.Album => await ProcessAlbumAsync(resourceId, cancellationToken),
            _ => Result<MusicSourceResult>.Failure("Unsupported Spotify resource type."),
        };
    }

    private async Task<Result<MusicSourceResult>> ProcessTrackAsync(string trackId,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = spotifyClientProvider.GetClient();
            var track = await client.Tracks.Get(trackId, cancellationToken);
            var spotifyTrack = SpotifyTrack.FromFullTrack(track);
            var resolved = await ResolveToYoutubeAsync(spotifyTrack, cancellationToken);

            if (resolved is null)
            {
                return Result<MusicSourceResult>.Failure(
                    $"Could not find a YouTube match for \"{spotifyTrack.Title}\" by {spotifyTrack.Artist}.");
            }

            return Result<MusicSourceResult>.Success(new MusicSourceResult([resolved]));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to resolve Spotify track {TrackId}.", trackId);
            return Result<MusicSourceResult>.Failure("Unable to fetch Spotify track metadata.");
        }
    }

    private async Task<Result<MusicSourceResult>> ProcessPlaylistAsync(string playlistId,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = spotifyClientProvider.GetClient();

            var spotifyTracks = new List<SpotifyTrack>();
            var playlist = await client.Playlists.Get(playlistId, cancellationToken);

            if (playlist.Items is null)
            {
                return Result<MusicSourceResult>.Failure("Spotify playlist is empty or inaccessible.");
            }

            await foreach (var item in client.Paginate(playlist.Items, cancel: cancellationToken))
            {
                if (options.CurrentValue.IsPlaylistLimitReached(spotifyTracks.Count))
                {
                    break;
                }

                if (item.Track is FullTrack track)
                {
                    spotifyTracks.Add(SpotifyTrack.FromFullTrack(track));
                }
            }

            return await ResolveTracksToYoutubeAsync(spotifyTracks, playlist.Name, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to resolve Spotify playlist {PlaylistId}.", playlistId);
            return Result<MusicSourceResult>.Failure("Unable to fetch Spotify playlist metadata.");
        }
    }

    private async Task<Result<MusicSourceResult>> ProcessAlbumAsync(string albumId,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = spotifyClientProvider.GetClient();

            var album = await client.Albums.Get(albumId, cancellationToken);
            var albumImageUrl = album.Images?.FirstOrDefault()?.Url;

            var spotifyTracks = new List<SpotifyTrack>();

            await foreach (var track in client.Paginate(album.Tracks, cancel: cancellationToken))
            {
                if (options.CurrentValue.IsPlaylistLimitReached(spotifyTracks.Count))
                {
                    break;
                }

                spotifyTracks.Add(SpotifyTrack.FromSimpleTrack(track, albumImageUrl));
            }

            return await ResolveTracksToYoutubeAsync(spotifyTracks, album.Name, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to resolve Spotify album {AlbumId}.", albumId);
            return Result<MusicSourceResult>.Failure("Unable to fetch Spotify album metadata.");
        }
    }

    private async Task<Result<MusicSourceResult>> ResolveTracksToYoutubeAsync(
        List<SpotifyTrack> spotifyTracks, string? collectionName, CancellationToken cancellationToken)
    {
        var results = new MusicSource?[spotifyTracks.Count];

        await Parallel.ForAsync(0, spotifyTracks.Count,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxYoutubeSearchConcurrency,
                CancellationToken = cancellationToken,
            },
            async (i, ct) => { results[i] = await ResolveToYoutubeAsync(spotifyTracks[i], ct); });

        var resolved = results.Where(r => r is not null).Cast<MusicSource>().ToList();
        var failedCount = spotifyTracks.Count - resolved.Count;

        if (failedCount > 0)
        {
            logger.LogWarning("Failed to resolve {FailedCount}/{TotalCount} Spotify tracks to YouTube.",
                failedCount, spotifyTracks.Count);
        }

        logger.LogInformation("Resolved {ResolvedCount}/{TotalCount} Spotify tracks to YouTube.",
            resolved.Count, spotifyTracks.Count);

        if (resolved.Count == 0)
        {
            return Result<MusicSourceResult>.Failure(
                "Could not find YouTube matches for any of the Spotify tracks.");
        }

        return Result<MusicSourceResult>.Success(new MusicSourceResult(resolved, collectionName));
    }

    private async Task<MusicSource?> ResolveToYoutubeAsync(SpotifyTrack spotifyTrack,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = $"{spotifyTrack.Artist} - {spotifyTrack.Title}";
            var searchResults = youtubeClient.Search.GetVideosAsync(query, cancellationToken);

            var firstResult = await searchResults.FirstOrDefaultAsync(cancellationToken);

            if (firstResult is null)
            {
                logger.LogWarning("No YouTube results for Spotify track \"{Title}\" by {Artist}.",
                    spotifyTrack.Title, spotifyTrack.Artist);
                return null;
            }

            var youtubeUrl = YoutubeHelpers.VideoUrl(firstResult.Id);

            return new MusicSource(spotifyTrack.Title, youtubeUrl, spotifyTrack.Artist, spotifyTrack.Duration,
                spotifyTrack.ImageUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "YouTube search failed for Spotify track \"{Title}\" by {Artist}.",
                spotifyTrack.Title, spotifyTrack.Artist);
            return null;
        }
    }

    private static bool TryParseSpotifyUrl(string url, out SpotifyResourceType resourceType, out string resourceId)
    {
        resourceType = default;
        resourceId = string.Empty;

        var match = SpotifyUrlPattern().Match(url);
        if (!match.Success)
        {
            return false;
        }

        var type = match.Groups["type"].Value;
        resourceId = match.Groups["id"].Value;

        resourceType = type.ToLowerInvariant() switch
        {
            "track" => SpotifyResourceType.Track,
            "playlist" => SpotifyResourceType.Playlist,
            "album" => SpotifyResourceType.Album,
            _ => default,
        };

        return resourceType is not default(SpotifyResourceType);
    }
}