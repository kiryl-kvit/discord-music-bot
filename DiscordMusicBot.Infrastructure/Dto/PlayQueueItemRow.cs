using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.Infrastructure.Dto;

internal sealed class PlayQueueItemRow
{
    public long Id { get; init; }
    public string GuildId { get; init; } = null!;
    public string UserId { get; init; } = null!;
    public string Url { get; init; } = null!;
    public string Title { get; init; } = null!;
    public string? Author { get; init; }
    public long? DurationMs { get; init; }
    public int Position { get; init; }

    public PlayQueueItem ToPlayQueueItem()
    {
        return PlayQueueItem.Restore(
            Id,
            ulong.Parse(GuildId),
            ulong.Parse(UserId),
            Url,
            Title,
            Author,
            DurationMs.HasValue ? TimeSpan.FromMilliseconds(DurationMs.Value) : null);
    }
}
