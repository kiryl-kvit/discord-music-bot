using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Services.Models;

public sealed record SkipResult(PlayQueueItem? Skipped, int TotalSkipped, PlayQueueItem? Next);
