using DiscordMusicBot.Core.Formatters;
using DiscordMusicBot.Domain.Playlists;

namespace DiscordMusicBot.App.Modules.Autocomplete;

public static class PlaylistAutocompleteHelper
{
    private const int MaxResults = 25;
    public const string PlaylistPrefix = "playlist:";
    private const int MaxLabelLength = 100;

    public static async Task<IReadOnlyList<Playlist>> SearchAsync(
        IPlaylistRepository repository, ulong userId, string input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? await repository.GetByUserAsync(userId, skip: 0, take: MaxResults)
            : await repository.SearchAsync(userId, input, MaxResults);
    }

    public static string FormatLabel(Playlist playlist)
    {
        var label = $"{playlist.Name} [{playlist.TrackCount} tracks]";

        if (playlist.TotalDurationMs.HasValue)
        {
            var duration = DateFormatter.FormatTime(TimeSpan.FromMilliseconds(playlist.TotalDurationMs.Value));
            label = $"{playlist.Name} [{playlist.TrackCount} tracks, {duration}]";
        }

        return label.Length > MaxLabelLength ? label[..(MaxLabelLength - 3)] + "..." : label;
    }
}