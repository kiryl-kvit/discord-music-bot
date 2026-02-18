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

    public long Position { get; private set; }

    public DateTime EnqueuedAtUtc { get; private set; } = DateTime.UtcNow;

    private PlayQueueItem()
    {
    }

    public static PlayQueueItem Create(ulong guildId, ulong userId, string url, string title, long position,
        string? author, TimeSpan? duration)
    {
        return new PlayQueueItem
        {
            GuildId = guildId,
            UserId = userId,
            Url = url.Trim(),
            Position = position,
            Title = title,
            Author = author,
            Duration = duration
        };
    }
}