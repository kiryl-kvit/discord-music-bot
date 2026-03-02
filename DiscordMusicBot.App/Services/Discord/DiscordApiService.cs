using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Services.Discord;

public sealed class DiscordApiService(DiscordSocketClient discordClient, ILogger<DiscordApiService> logger)
{
    private const string VoiceChannelStatusPrefix = "\ud83c\udfb5 ";

    public ulong BotUserId => discordClient.CurrentUser.Id;

    public async Task SetNowPlayingAsync(ulong? voiceChannelId, string trackTitle)
    {
        await SetBotActivityAsync(trackTitle);

        if (voiceChannelId is { } channelId)
        {
            await SetVoiceChannelStatusAsync(channelId, trackTitle);
        }
    }

    public async Task ClearNowPlayingAsync(ulong? voiceChannelId)
    {
        await ClearBotActivityAsync();

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

            var status = VoiceChannelStatusPrefix + trackTitle;
            if (status.Length > DiscordConfig.MaxVoiceChannelStatusLength)
            {
                status = status[..DiscordConfig.MaxVoiceChannelStatusLength];
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
}
