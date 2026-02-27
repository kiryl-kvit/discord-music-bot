using System.Globalization;
using DiscordMusicBot.Domain.Playlists;

namespace DiscordMusicBot.Infrastructure.Dto;

internal sealed class PlaylistRow
{
    public long Id { get; init; }
    public string UserId { get; init; } = null!;
    public string Name { get; init; } = null!;
    public int TrackCount { get; init; }
    public long? TotalDurationMs { get; init; }
    public string CreatedAt { get; init; } = null!;
    public string UpdatedAt { get; init; } = null!;

    public Playlist ToPlaylist()
    {
        return Playlist.Restore(
            Id,
            ulong.Parse(UserId),
            Name,
            TrackCount,
            TotalDurationMs,
            DateTime.Parse(CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
            DateTime.Parse(UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));
    }
}
