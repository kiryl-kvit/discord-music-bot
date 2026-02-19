using Discord;
using Discord.Interactions;
using DiscordMusicBot.App.Services;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Modules;

public class VoiceModule(
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
            await RespondAsync("You must be in a voice channel first. Join a voice channel and try again.", ephemeral: true);
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
                props.Content = "Failed to join the voice channel. Please check that the bot has permission to join and speak in the channel.");
        }
    }
}