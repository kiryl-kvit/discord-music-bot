namespace DiscordMusicBot.Domain.PlayQueue;

public interface IPlayQueueEventListener
{
    Task OnItemsAddedAsync(ulong guildId, IReadOnlyList<PlayQueueItem> items);

    Task OnItemsRemovedAsync(ulong guildId, IReadOnlyList<PlayQueueItem> items);
}
