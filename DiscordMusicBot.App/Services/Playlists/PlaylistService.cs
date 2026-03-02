using DiscordMusicBot.App.Options;
using DiscordMusicBot.Core;
using DiscordMusicBot.Domain.Playlists;
using Microsoft.Extensions.Options;

namespace DiscordMusicBot.App.Services.Playlists;

public sealed class PlaylistService(
    IPlaylistRepository playlistRepository,
    IOptionsMonitor<PlaylistsOptions> playlistsOptions)
{
    public async Task<Result<Playlist>> CreateAsync(
        ulong userId, string name,
        IReadOnlyList<PlaylistItem> items, long? totalDurationMs)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Playlist>.Failure("Playlist name cannot be empty.");
        }

        var trimmedName = name.Trim();
        var options = playlistsOptions.CurrentValue;

        var count = await playlistRepository.GetCountAsync(userId);

        if (options.IsLimitReached(count))
        {
            return Result<Playlist>.Failure(
                $"You have reached the playlist limit ({options.Limit}). " +
                "Delete some playlists before creating new ones.");
        }

        if (await playlistRepository.ExistsByNameAsync(userId, trimmedName))
        {
            return Result<Playlist>.Failure(
                $"A playlist named **{trimmedName}** already exists. " +
                "Choose a different name or delete the existing playlist first.");
        }

        var playlist = Playlist.Create(userId, trimmedName, items.Count, totalDurationMs);
        await playlistRepository.CreateAsync(playlist, items);

        return Result<Playlist>.Success(playlist);
    }

    public async Task<Result<Playlist>> AddItemAsync(Playlist playlist, PlaylistItem item)
    {
        var options = playlistsOptions.CurrentValue;

        if (options.IsItemLimitReached(playlist.TrackCount))
        {
            return Result<Playlist>.Failure(
                $"This playlist has reached the track limit ({options.ItemLimit}). " +
                "Remove some tracks or create a new playlist.");
        }

        await playlistRepository.AddItemAsync(playlist.Id, item);

        return Result<Playlist>.Success(playlist);
    }
}
