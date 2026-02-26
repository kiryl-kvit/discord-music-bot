using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordMusicBot.App.Services;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Modules;

public sealed class VoiceModule(
    VoiceConnectionService voiceConnectionService,
    ILogger<VoiceModule> logger) : InteractionModuleBase
{
    [SlashCommand("join", "Join the voice channel you are currently in", runMode: RunMode.Async)]
    public async Task JoinAsync()
    {
        var guildUser = Context.User as IGuildUser;
        var voiceChannel = guildUser?.VoiceChannel;

        if (voiceChannel is null)
        {
            await RespondAsync(
                "You must be in a voice channel first. " +
                "If you are in a private voice channel, make sure I have the **View Channel** permission on it.",
                ephemeral: true);
            return;
        }

        var botUser = ((SocketGuild)Context.Guild).CurrentUser;
        var permissions = botUser.GetPermissions(voiceChannel);

        if (!permissions.Connect || !permissions.Speak)
        {
            await RespondAsync(
                "I don't have permission to connect or speak in that voice channel. " +
                "Please grant me the **Connect** and **Speak** permissions on the channel.",
                ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        try
        {
            await voiceConnectionService.JoinAsync(voiceChannel);
            await ModifyOriginalResponseAsync(props =>
                props.Content = $"Joined **{voiceChannel.Name}**.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to join voice channel '{ChannelName}' ({ChannelId}) in guild {GuildId}",
                voiceChannel.Name, voiceChannel.Id, Context.Guild.Id);
            await ModifyOriginalResponseAsync(props =>
                props.Content =
                    "Failed to join the voice channel. Please check that the bot has permission to join and speak in the channel.");
        }
    }

    [SlashCommand("leave", "Leave the current voice channel", runMode: RunMode.Async)]
    public async Task LeaveAsync()
    {
        var guildUser = Context.User as IGuildUser;
        var voiceChannel = guildUser?.VoiceChannel;

        if (voiceChannel is null)
        {
            await RespondAsync(
                "You must be in a voice channel first. " +
                "If you are in a private voice channel, make sure I have the **View Channel** permission on it.",
                ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        try
        {
            await voiceConnectionService.LeaveAsync(voiceChannel);
            await ModifyOriginalResponseAsync(props =>
                props.Content = $"Left **{voiceChannel.Name}**.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to leave voice channel '{ChannelName}' ({ChannelId}) in guild {GuildId}",
                voiceChannel.Name, voiceChannel.Id, Context.Guild.Id);
            await ModifyOriginalResponseAsync(props =>
                props.Content =
                    "Failed to leave the voice channel. Please check that the bot has permission to leave the channel.");
        }
    }
}