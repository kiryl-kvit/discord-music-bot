using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordMusicBot.App.Services.Discord;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services.Voice;

public sealed class VoiceConnectionService(DiscordApiService discordApiService, ILogger<VoiceConnectionService> logger)
{
    private const int MaxReconnectAttempts = 5;
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16),
        TimeSpan.FromSeconds(30),
    ];

    private readonly ConcurrentDictionary<ulong, VoiceConnection> _connections = new();
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _reconnecting = new();

    public event Func<ulong, Task>? Connected;
    public event Func<ulong, Task>? Disconnected;
    public event Func<ulong, Task>? Reconnecting;

    public async Task<IAudioClient> JoinAsync(IVoiceChannel channel,
        CancellationToken cancellationToken = default)
    {
        var guildId = channel.GuildId;

        CancelReconnection(guildId);

        if (_connections.TryRemove(guildId, out var existing))
        {
            try
            {
                await existing.Client.StopAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error stopping existing audio client for guild {GuildId}", guildId);
            }
        }

        logger.LogInformation("Joining voice channel '{ChannelName}' ({ChannelId}) in guild {GuildId}",
            channel.Name, channel.Id, guildId);

        IAudioClient audioClient;

        try
        {
            audioClient = await EstablishConnectionAsync(channel, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"Timed out connecting to voice channel '{channel.Name}' ({channel.Id}). " +
                "This usually means the bot lacks the Connect permission on the channel.");
        }

        logger.LogInformation("Connected to voice channel '{ChannelName}' ({ChannelId}) in guild {GuildId}",
            channel.Name, channel.Id, guildId);

        await RaiseEventAsync(Connected, guildId);

        return audioClient;
    }

    public async Task LeaveAsync(IVoiceChannel channel, CancellationToken cancellationToken = default)
    {
        var guildId = channel.GuildId;

        CancelReconnection(guildId);

        if (_connections.TryRemove(guildId, out var existing))
        {
            await RaiseEventAsync(Disconnected, guildId);

            try
            {
                await existing.Client.StopAsync();
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
    }

    private async Task RaiseEventAsync(Func<ulong, Task>? handler, ulong guildId)
    {
        if (handler is not null)
        {
            await handler.Invoke(guildId);
        }
    }

    private async Task<IAudioClient> EstablishConnectionAsync(IVoiceChannel channel,
        CancellationToken cancellationToken)
    {
        var guildId = channel.GuildId;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var audioClient = await channel.ConnectAsync(selfDeaf: true).WaitAsync(cts.Token);

        audioClient.Disconnected += exception => OnAudioClientDisconnected(guildId, exception);
        _connections[guildId] = new VoiceConnection(audioClient, channel.Id);

        return audioClient;
    }

    public IAudioClient? GetConnection(ulong guildId)
    {
        return _connections.TryGetValue(guildId, out var connection) ? connection.Client : null;
    }

    public ulong? GetVoiceChannelId(ulong guildId)
    {
        return _connections.TryGetValue(guildId, out var connection) ? connection.ChannelId : null;
    }

    public async Task DisconnectAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        CancelReconnection(guildId);

        if (!_connections.TryRemove(guildId, out var connection))
        {
            return;
        }

        try
        {
            await connection.Client.StopAsync();
            logger.LogInformation("Disconnected from voice in guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error disconnecting audio client for guild {GuildId}", guildId);
        }
    }

    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        var guildIds = _connections.Keys.ToArray();

        foreach (var guildId in guildIds)
        {
            await DisconnectAsync(guildId, cancellationToken);
        }
    }

    public Task HandleVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        return user.Id != discordApiService.BotUserId ? Task.CompletedTask : HandleBotVoiceStateUpdatedAsync(before, after);
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

                if (_connections.TryGetValue(afterChannel.Guild.Id, out var conn))
                {
                    _connections[afterChannel.Guild.Id] = conn with { ChannelId = afterChannel.Id };
                }
            }
        }
        else if (beforeChannel is not null && afterChannel is null)
        {
            var guildId = beforeChannel.Guild.Id;

            if (_reconnecting.ContainsKey(guildId))
            {
                logger.LogInformation(
                    "Bot left voice channel '{ChannelName}' ({ChannelId}) in guild {GuildId} " +
                    "during reconnection attempt — suppressing disconnect event",
                    beforeChannel.Name, beforeChannel.Id, guildId);
                return;
            }

            logger.LogInformation(
                "Bot was disconnected from voice channel '{ChannelName}' ({ChannelId}) in guild {GuildId}",
                beforeChannel.Name, beforeChannel.Id, guildId);

            _connections.TryRemove(guildId, out _);

            await RaiseEventAsync(Disconnected, guildId);
        }
    }

    private async Task OnAudioClientDisconnected(ulong guildId, Exception exception)
    {
        if (!_connections.TryGetValue(guildId, out var connection))
        {
            return;
        }

        if (_reconnecting.ContainsKey(guildId))
        {
            return;
        }

        logger.LogWarning(exception, "Audio client disconnected unexpectedly in guild {GuildId}. " +
                                     "Starting reconnection", guildId);

        var channelId = connection.ChannelId;

        await RaiseEventAsync(Reconnecting, guildId);

        _ = Task.Run(() => ReconnectAsync(guildId, channelId));
    }

    private async Task ReconnectAsync(ulong guildId, ulong channelId)
    {
        var cts = new CancellationTokenSource();

        if (!_reconnecting.TryAdd(guildId, cts))
        {
            cts.Dispose();
            return;
        }

        if (_connections.TryRemove(guildId, out var stale))
        {
            try
            {
                await stale.Client.StopAsync();
            }
            catch
            {
            }
        }

        try
        {
            var token = cts.Token;

            for (var attempt = 0; attempt < MaxReconnectAttempts; attempt++)
            {
                var delay = ReconnectDelays[Math.Min(attempt, ReconnectDelays.Length - 1)];

                logger.LogInformation(
                    "Reconnect attempt {Attempt}/{Max} for guild {GuildId} in {Delay}s",
                    attempt + 1, MaxReconnectAttempts, guildId, delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation(
                        "Reconnection cancelled for guild {GuildId} during backoff delay", guildId);
                    return;
                }

                var channel = discordApiService.GetVoiceChannel(guildId, channelId);
                if (channel is null)
                {
                    logger.LogWarning("Voice channel {ChannelId} no longer exists in guild {GuildId}. " +
                                      "Aborting reconnection", channelId, guildId);
                    break;
                }

                try
                {
                    await EstablishConnectionAsync(channel, token);

                    logger.LogInformation(
                        "Successfully reconnected to voice channel '{ChannelName}' ({ChannelId}) " +
                        "in guild {GuildId} on attempt {Attempt}",
                        channel.Name, channel.Id, guildId, attempt + 1);

                    _reconnecting.TryRemove(guildId, out _);
                    cts.Dispose();

                    await RaiseEventAsync(Connected, guildId);
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    logger.LogInformation(
                        "Reconnection cancelled for guild {GuildId} during connect attempt", guildId);
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Reconnect attempt {Attempt}/{Max} failed for guild {GuildId}",
                        attempt + 1, MaxReconnectAttempts, guildId);
                }
            }

            logger.LogError(
                "All {Max} reconnect attempts failed for guild {GuildId}. Giving up",
                MaxReconnectAttempts, guildId);
        }
        finally
        {
            if (_reconnecting.TryRemove(guildId, out var removed))
            {
                removed.Dispose();
            }
        }

        try
        {
            var channel = discordApiService.GetVoiceChannel(guildId, channelId);
            if (channel is not null)
            {
                await channel.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error disconnecting from voice channel after failed " +
                                  "reconnection in guild {GuildId}", guildId);
        }

        _connections.TryRemove(guildId, out _);
        await RaiseEventAsync(Disconnected, guildId);
    }

    private void CancelReconnection(ulong guildId)
    {
        if (_reconnecting.TryRemove(guildId, out var cts))
        {
            logger.LogInformation("Cancelling ongoing reconnection for guild {GuildId}", guildId);
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
