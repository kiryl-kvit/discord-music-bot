using DiscordMusicBot.Domain.Playlists;

namespace DiscordMusicBot.Infrastructure.Dto;

internal sealed class PlaylistItemRow
{
    public long Id { get; init; }
    public long PlaylistId { get; init; }
    public int Position { get; init; }
    public string Url { get; init; } = null!;
    public string Title { get; init; } = null!;
    public string? Author { get; init; }
    public long? DurationMs { get; init; }
    public string? ThumbnailUrl { get; init; }

    public PlaylistItem ToPlaylistItem()
    {
        return PlaylistItem.Restore(
            Id,
            PlaylistId,
            Position,
            Url,
            Title,
            Author,
            DurationMs,
            ThumbnailUrl);
    }
}
