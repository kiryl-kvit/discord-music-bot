using System.Globalization;
using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.Infrastructure.Dto;

internal sealed class PlayQueueItemRow
{
    public long Id { get; init; }
    public string GuildId { get; init; } = null!;
    public string UserId { get; init; } = null!;
    public string SourceType { get; init; } = null!;
    public string Url { get; init; } = null!;
    public string Title { get; init; } = null!;
    public string? Author { get; init; }
    public long? DurationMs { get; init; }
    public string? ThumbnailUrl { get; init; }
    public int Position { get; init; }
    public string? PlayedAt { get; init; }

    public PlayQueueItem ToPlayQueueItem()
    {
        return PlayQueueItem.Restore(
            Id,
            ulong.Parse(GuildId),
            ulong.Parse(UserId),
            Enum.Parse<DiscordMusicBot.Core.MusicSource.SourceType>(SourceType),
            Url,
            Title,
            Author,
            DurationMs.HasValue ? TimeSpan.FromMilliseconds(DurationMs.Value) : null,
            ThumbnailUrl,
            PlayedAt is not null
                ? DateTime.Parse(PlayedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                : null);
    }
}
