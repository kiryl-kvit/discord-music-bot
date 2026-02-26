using Discord;
using Discord.Interactions;

namespace DiscordMusicBot.App.Modules;

public sealed class HelpModule : InteractionModuleBase
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
            .AddField("/queue add `<url>`", "Add a URL or a favorite to the queue. Start typing to see your favorites.", inline: false)
            .AddField("/queue resume", "Resume queue playback.", inline: false)
            .AddField("/queue pause", "Pause queue playback.", inline: false)
            .AddField("/queue shuffle", "Shuffle the current queue.", inline: false)
            .AddField("/queue skip `[count]`", "Skip one or more tracks. Defaults to 1.", inline: false)
            .AddField("/queue list `[page]`", "Show the current queue with pagination.", inline: false)
            .AddField("/queue clear", "Clear all items from the queue.", inline: false)
            .AddField("/fav add `<url>` `[alias]`", "Save a track or playlist to your favorites.", inline: false)
            .AddField("/fav list `[page]`", "Show your favorites with pagination.", inline: false)
            .AddField("/fav remove `<favorite>`", "Remove a favorite by name (autocomplete).", inline: false)
            .AddField("/fav rename `<favorite>` `<new_name>`", "Rename a favorite (autocomplete).", inline: false)
            .Build();
    }
}
