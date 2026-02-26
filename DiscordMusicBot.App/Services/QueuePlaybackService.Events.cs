using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services;

public sealed partial class QueuePlaybackService
{
    public async Task OnVoiceConnected(ulong guildId)
    {
        logger.LogInformation("Voice connected in guild {GuildId}. Starting the playback", guildId);

        var state = GetState(guildId);
        var channelId = voiceConnectionService.GetVoiceChannelId(guildId);

        if (channelId is not null)
        {
            state.VoiceChannelId = channelId;
        }

        state.IsConnected = true;

        await StartAsync(guildId);
    }

    public async Task OnVoiceDisconnected(ulong guildId)
    {
        logger.LogInformation("Voice disconnected in guild {GuildId}. Stopping playback.", guildId);

        var state = GetState(guildId);
        state.IsConnected = false;

        await DeleteNowPlayingAsync(guildId, CancellationToken.None);
        await PauseAsync(guildId);

        state.VoiceChannelId = null;
        await ClearPersistedStateAsync(guildId, CancellationToken.None);
    }
}