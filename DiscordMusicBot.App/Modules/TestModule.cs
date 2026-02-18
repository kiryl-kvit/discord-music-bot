using Discord.Interactions;

namespace DiscordMusicBot.App.Modules;

public class TestModule : InteractionModuleBase
{
    [SlashCommand("echo", "Echo an input")]
    public async Task Echo(string input)
    {
        await RespondAsync(input);
    }
}