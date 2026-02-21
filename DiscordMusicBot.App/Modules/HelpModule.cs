using Discord;
using Discord.Interactions;

namespace DiscordMusicBot.App.Modules;

public class HelpModule : InteractionModuleBase
{
    [SlashCommand("help", "Show a list of all available commands")]
    public async Task HelpAsync()
    {
        var embed = BuildHelpEmbed();
        await RespondAsync(embed: embed, ephemeral: true);
    }

    private static Embed BuildHelpEmbed()
    {
        return new EmbedBuilder()
            .WithTitle("Music Bot Commands")
            .WithColor(Color.Purple)
            .WithDescription("Here are all the available commands:")
            .AddField("/help", "Show this help message.", inline: false)
            .AddField("/join", "Join the voice channel you are currently in.", inline: false)
            .AddField("/leave", "Leave the current voice channel.", inline: false)
            .AddField("/queue add `<url>`", "Add a YouTube video or playlist to the queue.", inline: false)
            .AddField("/queue start", "Start or resume queue playback.", inline: false)
            .AddField("/queue pause", "Pause queue playback.", inline: false)
            .AddField("/queue skip", "Skip the current track.", inline: false)
            .AddField("/queue list `[page]`", "Show the current queue with pagination.", inline: false)
            .AddField("/queue clear", "Clear all items from the queue.", inline: false)
            .Build();
    }
}
