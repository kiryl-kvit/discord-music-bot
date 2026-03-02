using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services.Discord;

public sealed class DiscordApiService(DiscordSocketClient discordClient, ILogger<DiscordApiService> logger)
{
    private readonly ConcurrentDictionary<ulong, string> _activeGuilds = new();

    public ulong BotUserId => discordClient.CurrentUser.Id;

    public async Task SetNowPlayingAsync(ulong guildId, ulong? voiceChannelId, string trackTitle)
    {
        _activeGuilds[guildId] = trackTitle;
        await SetBotActivityAsync(trackTitle);

        if (voiceChannelId is { } channelId)
        {
            await SetVoiceChannelStatusAsync(channelId, trackTitle);
        }
    }

    public async Task ClearNowPlayingAsync(ulong guildId, ulong? voiceChannelId)
    {
        _activeGuilds.TryRemove(guildId, out _);

        if (!_activeGuilds.IsEmpty)
        {
            var (_, otherTitle) = _activeGuilds.First();
            await SetBotActivityAsync(otherTitle);
        }
        else
        {
            await ClearBotActivityAsync();
        }

        if (voiceChannelId is { } channelId)
        {
            await ClearVoiceChannelStatusAsync(channelId);
        }
    }

    public IVoiceChannel? GetVoiceChannel(ulong guildId, ulong channelId)
    {
        return discordClient.GetGuild(guildId)?.GetVoiceChannel(channelId);
    }

    public IMessageChannel? GetTextChannel(ulong guildId, ulong channelId)
    {
        return discordClient.GetGuild(guildId)?.GetTextChannel(channelId);
    }

    private async Task SetBotActivityAsync(string trackTitle)
    {
        try
        {
            await discordClient.SetActivityAsync(new Game(trackTitle, ActivityType.Listening));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set activity status");
        }
    }

    private async Task ClearBotActivityAsync()
    {
        try
        {
            await discordClient.SetActivityAsync(null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear activity status");
        }
    }

    private async Task SetVoiceChannelStatusAsync(ulong channelId, string trackTitle)
    {
        try
        {
            var channel = discordClient.GetChannel(channelId) as IVoiceChannel;
            if (channel is null)
            {
                return;
            }

            var status = trackTitle;
            if (status.Length > DiscordConfig.MaxVoiceChannelStatusLength)
            {
                status = TruncateUnicodeSafe(status, DiscordConfig.MaxVoiceChannelStatusLength);
            }

            await channel.SetStatusAsync(status);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set voice channel status for channel {ChannelId}", channelId);
        }
    }

    private async Task ClearVoiceChannelStatusAsync(ulong channelId)
    {
        try
        {
            var channel = discordClient.GetChannel(channelId) as IVoiceChannel;
            if (channel is null)
            {
                return;
            }

            await channel.SetStatusAsync("");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear voice channel status for channel {ChannelId}", channelId);
        }
    }

    private static string TruncateUnicodeSafe(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var end = maxLength;
        if (end > 0 && char.IsHighSurrogate(value[end - 1]))
        {
            end--;
        }

        return value[..end];
    }
}
