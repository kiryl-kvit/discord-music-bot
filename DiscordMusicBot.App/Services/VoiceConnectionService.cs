using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services;

public sealed class VoiceConnectionService(DiscordSocketClient client, ILogger<VoiceConnectionService> logger)
{
    private readonly ConcurrentDictionary<ulong, IAudioClient> _connections = new();

    public event Func<ulong, Task>? Connected;
    public event Func<ulong, Task>? Disconnected;

    public async Task<IAudioClient> JoinAsync(IVoiceChannel channel)
    {
        var guildId = channel.GuildId;

        if (_connections.TryRemove(guildId, out var existing))
        {
            try
            {
                await existing.StopAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error stopping existing audio client for guild {GuildId}", guildId);
            }
        }

        logger.LogInformation("Joining voice channel '{ChannelName}' ({ChannelId}) in guild {GuildId}",
            channel.Name, channel.Id, guildId);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        IAudioClient audioClient;

        try
        {
            audioClient = await channel.ConnectAsync(selfDeaf: true).WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"Timed out connecting to voice channel '{channel.Name}' ({channel.Id}). " +
                "This usually means the bot lacks the Connect permission on the channel.");
        }

        audioClient.Disconnected += exception => OnAudioClientDisconnected(guildId, exception);

        _connections[guildId] = audioClient;

        logger.LogInformation("Connected to voice channel '{ChannelName}' ({ChannelId}) in guild {GuildId}",
            channel.Name, channel.Id, guildId);

        await NotifyConnectedAsync(guildId);

        return audioClient;
    }

    public async Task LeaveAsync(IVoiceChannel channel)
    {
        var guildId = channel.GuildId;

        if (_connections.TryRemove(guildId, out var existing))
        {
            try
            {
                await existing.StopAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error stopping existing audio client for guild {GuildId}", guildId);
            }
        }

        logger.LogInformation("Leaving voice channel '{ChannelName}' ({ChannelId}) in guild {GuildId}",
            channel.Name, channel.Id, guildId);

        await channel.DisconnectAsync();

        logger.LogInformation("Disconnected from voice channel '{ChannelName}' ({ChannelId}) in guild {GuildId}",
            channel.Name, channel.Id, guildId);

        await NotifyDisconnectedAsync(guildId);
    }

    private async Task NotifyConnectedAsync(ulong guildId)
    {
        if (Connected is not null)
        {
            await Connected.Invoke(guildId);
        }
    }

    private async Task NotifyDisconnectedAsync(ulong guildId)
    {
        if (Disconnected is not null)
        {
            await Disconnected.Invoke(guildId);
        }
    }

    public IAudioClient? GetConnection(ulong guildId)
    {
        return _connections.GetValueOrDefault(guildId);
    }

    public async Task DisconnectAsync(ulong guildId)
    {
        if (!_connections.TryRemove(guildId, out var audioClient))
        {
            return;
        }

        try
        {
            await audioClient.StopAsync();
            logger.LogInformation("Disconnected from voice in guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error disconnecting audio client for guild {GuildId}", guildId);
        }
    }

    public Task HandleVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        return user.Id != client.CurrentUser.Id ? Task.CompletedTask : HandleBotVoiceStateUpdatedAsync(before, after);
    }

    private async Task HandleBotVoiceStateUpdatedAsync(SocketVoiceState before, SocketVoiceState after)
    {
        var beforeChannel = before.VoiceChannel;
        var afterChannel = after.VoiceChannel;

        if (beforeChannel is not null && afterChannel is not null)
        {
            if (beforeChannel.Id != afterChannel.Id)
            {
                logger.LogInformation(
                    "Bot was moved from '{BeforeChannel}' ({BeforeId}) to '{AfterChannel}' ({AfterId}) in guild {GuildId}",
                    beforeChannel.Name, beforeChannel.Id,
                    afterChannel.Name, afterChannel.Id,
                    afterChannel.Guild.Id);
            }
        }
        else if (beforeChannel is not null && afterChannel is null)
        {
            var guildId = beforeChannel.Guild.Id;
            logger.LogInformation(
                "Bot was disconnected from voice channel '{ChannelName}' ({ChannelId}) in guild {GuildId}",
                beforeChannel.Name, beforeChannel.Id, guildId);

            _connections.TryRemove(guildId, out _);

            await NotifyDisconnectedAsync(guildId);
        }
    }

    private Task OnAudioClientDisconnected(ulong guildId, Exception exception)
    {
        // The IAudioClient.Disconnected event fires on transport-level disconnects.
        // The UserVoiceStateUpdated handler above covers intentional disconnect/kicks,
        // so this primarily handles unexpected transport failures.
        logger.LogWarning(exception, "Audio client disconnected unexpectedly in guild {GuildId}", guildId);

        return Task.CompletedTask;
    }
}