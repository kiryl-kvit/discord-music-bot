using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services.Queue;

public sealed partial class QueuePlaybackService
{
    public event Func<ulong, PlayQueueItem, Task>? TrackStarted;
    public event Func<ulong, Task>? PlaybackPaused;
    public event Func<ulong, Task>? PlaybackResumed;
    public event Func<ulong, Task>? PlaybackStopped;
    public event Func<ulong, Task>? TrackLoading;

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
        state.IsReconnecting = false;

        await StartAsync(guildId);
    }

    public async Task OnVoiceDisconnected(ulong guildId)
    {
        var state = GetState(guildId);
        var wasReconnecting = state.IsReconnecting;
        state.IsConnected = false;
        state.IsReconnecting = false;

        if (wasReconnecting)
        {
            logger.LogInformation("Reconnection failed in guild {GuildId}. Stopping playback.", guildId);
            await SendFeedbackAsync(guildId,
                "Voice connection lost",
                "The audio connection was dropped and all reconnection attempts failed. " +
                "Use `/join` to reconnect and `/queue resume` to restart playback.",
                CancellationToken.None);
        }
        else
        {
            logger.LogInformation("Voice disconnected in guild {GuildId}. Stopping playback.", guildId);
        }

        await PauseAsync(guildId);

        state.VoiceChannelId = null;
        await ClearPersistedStateAsync(guildId, CancellationToken.None);
    }

    public async Task OnVoiceReconnecting(ulong guildId)
    {
        logger.LogInformation("Voice reconnecting in guild {GuildId}. Pausing playback until reconnected.", guildId);

        var state = GetState(guildId);
        state.IsConnected = false;
        state.IsReconnecting = true;

        await PauseAsync(guildId);
    }

    private async Task RaiseTrackStartedAsync(ulong guildId, PlayQueueItem item)
    {
        await RaiseEventAsync(TrackStarted, guildId, item, nameof(TrackStarted));
    }

    private async Task RaisePlaybackPausedAsync(ulong guildId)
    {
        await RaiseEventAsync(PlaybackPaused, guildId, nameof(PlaybackPaused));
    }

    private async Task RaisePlaybackResumedAsync(ulong guildId)
    {
        await RaiseEventAsync(PlaybackResumed, guildId, nameof(PlaybackResumed));
    }

    private async Task RaisePlaybackStoppedAsync(ulong guildId)
    {
        await RaiseEventAsync(PlaybackStopped, guildId, nameof(PlaybackStopped));
    }

    private async Task RaiseTrackLoadingAsync(ulong guildId)
    {
        await RaiseEventAsync(TrackLoading, guildId, nameof(TrackLoading));
    }

    private async Task RaiseEventAsync(Func<ulong, Task>? handler, ulong guildId, string eventName)
    {
        try
        {
            if (handler is not null)
            {
                await handler.Invoke(guildId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error in {EventName} event handler for guild {GuildId}", eventName, guildId);
        }
    }

    private async Task RaiseEventAsync<T>(Func<ulong, T, Task>? handler, ulong guildId, T arg, string eventName)
    {
        try
        {
            if (handler is not null)
            {
                await handler.Invoke(guildId, arg);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error in {EventName} event handler for guild {GuildId}", eventName, guildId);
        }
    }
}
