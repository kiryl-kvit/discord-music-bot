namespace DiscordMusicBot.Domain.PlayQueue.Dto;

public record EnqueueItemDto(ulong GuildId, ulong UserId, string Url, string Title, string? Author, TimeSpan? Duration);