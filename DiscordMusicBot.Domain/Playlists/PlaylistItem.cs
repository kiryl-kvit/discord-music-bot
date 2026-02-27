namespace DiscordMusicBot.Domain.Playlists;

public sealed class PlaylistItem
{
    public long Id { get; private set; }

    public long PlaylistId { get; private set; }
    public int Position { get; private set; }

    public string Url { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Author { get; private set; }
    public long? DurationMs { get; private set; }
    public string? ThumbnailUrl { get; private set; }

    private PlaylistItem()
    {
    }

    public static PlaylistItem Create(int position, string url, string title, string? author,
        long? durationMs, string? thumbnailUrl)
    {
        return new PlaylistItem
        {
            Position = position,
            Url = url,
            Title = title,
            Author = author,
            DurationMs = durationMs,
            ThumbnailUrl = thumbnailUrl,
        };
    }

    public static PlaylistItem Restore(long id, long playlistId, int position, string url, string title,
        string? author, long? durationMs, string? thumbnailUrl)
    {
        return new PlaylistItem
        {
            Id = id,
            PlaylistId = playlistId,
            Position = position,
            Url = url,
            Title = title,
            Author = author,
            DurationMs = durationMs,
            ThumbnailUrl = thumbnailUrl,
        };
    }
}