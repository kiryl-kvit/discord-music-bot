namespace DiscordMusicBot.Domain.PlayQueue;

public sealed class PlayQueueItem
{
    public long Id { get; private set; }

    public ulong GuildId { get; private set; }
    public ulong UserId { get; private set; }

    public string Url { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Author { get; private set; }
    public TimeSpan? Duration { get; private set; }

    private PlayQueueItem()
    {
    }

    public static PlayQueueItem Create(ulong guildId, ulong userId, string url, string title, string? author,
        TimeSpan? duration)
    {
        return new PlayQueueItem
        {
            GuildId = guildId,
            UserId = userId,
            Url = url.Trim(),
            Title = title,
            Author = author,
            Duration = duration
        };
    }

    public static PlayQueueItem Restore(long id, ulong guildId, ulong userId, string url, string title,
        string? author, TimeSpan? duration)
    {
        return new PlayQueueItem
        {
            Id = id,
            GuildId = guildId,
            UserId = userId,
            Url = url,
            Title = title,
            Author = author,
            Duration = duration
        };
    }

    public void SetId(long id)
    {
        Id = id;
    }
}