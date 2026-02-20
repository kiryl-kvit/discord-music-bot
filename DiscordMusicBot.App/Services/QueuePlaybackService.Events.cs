using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services;

public sealed partial class QueuePlaybackService
{
    public Task OnVoiceConnected(ulong guildId)
    {
        logger.LogInformation("Voice connected in guild {GuildId}. Starting the playback", guildId);
        GetState(guildId).IsConnected = true;
        Start(guildId);
        return Task.CompletedTask;
    }

    public async Task OnVoiceDisconnected(ulong guildId)
    {
        logger.LogInformation("Voice disconnected in guild {GuildId}. Stopping playback.", guildId);

        GetState(guildId).IsConnected = false;
        await StopAsync(guildId);
    }

    public Task OnItemsAddedAsync(ulong guildId, IReadOnlyList<PlayQueueItem> items)
    {
        var state = GetState(guildId);
        state.Items.AddRange(items);
        state.Items = state.Items.OrderBy(x => x.Position).ToList();

        if (state.IsConnected)
        {
            Start(guildId);
        }
        
        return Task.CompletedTask;
    }

    public Task OnItemsRemovedAsync(ulong guildId, IReadOnlyList<PlayQueueItem> items)
    {
        return Task.CompletedTask;
    }
}