using Discord;
using Discord.Interactions;
using DiscordMusicBot.App.Services.Common;
using DiscordMusicBot.App.Services.NowPlaying;

namespace DiscordMusicBot.App.Modules.NowPlaying;

[Group("now", "Now playing controls")]
public sealed class NowPlayingControlsModule(
    NowPlayingMessageService nowPlayingMessageService) : InteractionModuleBase
{
    [SlashCommand("start", "Show and track the currently playing track")]
    public async Task StartAsync()
    {
        var guildId = Context.Guild.Id;

        var info = await nowPlayingMessageService.BuildNowPlayingInfoAsync(guildId);
        if (info is null)
        {
            var errorEmbed = ErrorEmbedBuilder.Build(
                "Nothing playing",
                "There is no track currently playing.",
                "Use `/queue add <url>` to add a track.");
            await RespondAsync(embed: errorEmbed);
            return;
        }

        var embed = NowPlayingEmbedBuilder.BuildEmbed(info);
        var components = NowPlayingEmbedBuilder.BuildComponents(info.IsPaused);
        await RespondAsync(embed: embed, components: components);

        var response = await GetOriginalResponseAsync();
        nowPlayingMessageService.RegisterCommandResponse(guildId, Context.Channel, response.Id);
    }

    [SlashCommand("stop", "Stop the live now-playing message")]
    public async Task StopAsync()
    {
        var guildId = Context.Guild.Id;

        var stopped = await nowPlayingMessageService.StopAsync(guildId);
        if (!stopped)
        {
            var errorEmbed = ErrorEmbedBuilder.Build(
                "No active now-playing message",
                "There is no live now-playing message to stop.",
                "Use `/now start` to start one.");
            await RespondAsync(embed: errorEmbed, ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("Now Playing Stopped")
            .WithColor(Color.Green)
            .WithDescription("The live now-playing message has been stopped.")
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }
}
