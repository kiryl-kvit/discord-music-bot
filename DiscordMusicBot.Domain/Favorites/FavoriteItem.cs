namespace DiscordMusicBot.Domain.Favorites;

public sealed class FavoriteItem
{
    public long Id { get; private set; }

    public ulong UserId { get; private set; }

    public string Url { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Alias { get; private set; }
    public string? Author { get; private set; }
    public TimeSpan? Duration { get; private set; }
    public bool IsPlaylist { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public string DisplayName => Alias ?? Title;

    private FavoriteItem()
    {
    }

    public static FavoriteItem Create(ulong userId, string url, string title, string? alias, string? author,
        TimeSpan? duration, bool isPlaylist)
    {
        return new FavoriteItem
        {
            UserId = userId,
            Url = url.Trim(),
            Title = title,
            Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim(),
            Author = author,
            Duration = duration,
            IsPlaylist = isPlaylist,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static FavoriteItem Restore(long id, ulong userId, string url, string title, string? alias,
        string? author, TimeSpan? duration, bool isPlaylist, DateTime createdAt)
    {
        return new FavoriteItem
        {
            Id = id,
            UserId = userId,
            Url = url,
            Title = title,
            Alias = alias,
            Author = author,
            Duration = duration,
            IsPlaylist = isPlaylist,
            CreatedAt = createdAt,
        };
    }
}
