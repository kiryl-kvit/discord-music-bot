using Discord.Interactions;
using DiscordMusicBot.App.Services;

namespace DiscordMusicBot.App.Modules;

public sealed class NowPlayingControlsModule(QueuePlaybackService queuePlaybackService) : InteractionModuleBase
{
    [SlashCommand("now", "Show the currently playing track")]
    public async Task NowPlayingAsync()
    {
        var guildId = Context.Guild.Id;
        var currentItem = queuePlaybackService.GetCurrentItem(guildId);

        if (currentItem is null)
        {
            var errorEmbed = ErrorEmbedBuilder.Build(
                "Nothing playing",
                "There is no track currently playing.",
                "Use `/queue add <url>` to add a track.");
            await RespondAsync(embed: errorEmbed);
            return;
        }

        var isPaused = !queuePlaybackService.IsPlaying(guildId);
        var embed = NowPlayingEmbedBuilder.BuildEmbed(currentItem, isPaused);
        await RespondAsync(embed: embed);
    }
}
