using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services;

public sealed partial class QueuePlaybackService
{
    public async Task OnVoiceConnected(ulong guildId)
    {
        logger.LogInformation("Voice connected in guild {GuildId}. Starting the playback", guildId);

        await StartAsync(guildId);
    }

    public async Task OnVoiceDisconnected(ulong guildId)
    {
        logger.LogInformation("Voice disconnected in guild {GuildId}. Stopping playback.", guildId);

        await StopAsync(guildId);
    }
}