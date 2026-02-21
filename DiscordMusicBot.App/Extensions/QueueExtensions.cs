using DiscordMusicBot.Domain.PlayQueue;

namespace DiscordMusicBot.App.Extensions;

public static class QueueExtensions
{
    extension(List<PlayQueueItem> items)
    {
        public PlayQueueItem? Pop()
        {
            if (items.Count == 0)
            {
                return null;
            }

            var item = items[0];
            items.RemoveAt(0);
            return item;
        }
    }
}