using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services.Queue;

public sealed record SkipResult(PlayQueueItem? Skipped, int TotalSkipped, PlayQueueItem? Next);
