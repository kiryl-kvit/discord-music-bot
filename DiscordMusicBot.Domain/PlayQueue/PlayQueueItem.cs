namespace DiscordMusicBot.Domain.PlayQueue;

public sealed class PlayQueueItem
{
    public long Id { get; set; }

    public ulong GuildId { get; set; }

    public PlayQueueItemType Type { get; set; }

    public required string Source { get; set; } = null!;

    public long Position { get; set; }

    public DateTime EnqueuedAtUtc { get; set; }
}
