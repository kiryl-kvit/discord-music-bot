using System.Globalization;
using DiscordMusicBot.Domain.Favorites;

namespace DiscordMusicBot.Infrastructure.Dto;

internal sealed class FavoriteItemRow
{
    public long Id { get; init; }
    public string UserId { get; init; } = null!;
    public string Url { get; init; } = null!;
    public string Title { get; init; } = null!;
    public string? Alias { get; init; }
    public string? Author { get; init; }
    public long? DurationMs { get; init; }
    public int IsPlaylist { get; init; }
    public string CreatedAt { get; init; } = null!;

    public FavoriteItem ToFavoriteItem()
    {
        return FavoriteItem.Restore(
            Id,
            ulong.Parse(UserId),
            Url,
            Title,
            Alias,
            Author,
            DurationMs.HasValue ? TimeSpan.FromMilliseconds(DurationMs.Value) : null,
            IsPlaylist != 0,
            DateTime.Parse(CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));
    }
}
