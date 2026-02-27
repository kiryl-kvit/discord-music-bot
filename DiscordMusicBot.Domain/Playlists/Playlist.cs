namespace DiscordMusicBot.Domain.Playlists;

public sealed class Playlist
{
    public long Id { get; private set; }

    public ulong UserId { get; private set; }

    public string Name { get; private set; } = null!;
    public int TrackCount { get; private set; }
    public long? TotalDurationMs { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Playlist()
    {
    }

    public void Rename(string newName)
    {
        Name = newName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public static Playlist Create(ulong userId, string name, int trackCount, long? totalDurationMs)
    {
        var now = DateTime.UtcNow;
        return new Playlist
        {
            UserId = userId,
            Name = name.Trim(),
            TrackCount = trackCount,
            TotalDurationMs = totalDurationMs,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public static Playlist Restore(long id, ulong userId, string name, int trackCount,
        long? totalDurationMs, DateTime createdAt, DateTime updatedAt)
    {
        return new Playlist
        {
            Id = id,
            UserId = userId,
            Name = name,
            TrackCount = trackCount,
            TotalDurationMs = totalDurationMs,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
    }
}