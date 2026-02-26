using Discord;

namespace DiscordMusicBot.App.Services;

public static class ErrorEmbedBuilder
{
    public static Embed Build(string title, string description, string? guidance = null)
    {
        var builder = new EmbedBuilder()
            .WithColor(Color.Red)
            .WithTitle(title)
            .WithDescription(description);

        if (guidance is not null)
        {
            builder.AddField("What to do", guidance);
        }

        return builder.Build();
    }
}
