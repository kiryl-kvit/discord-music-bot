using Discord;
using Discord.Interactions;

namespace DiscordMusicBot.App.Modules.Common;

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
            .WithDescription(
                "**Getting Started**\n" +
                "1. Join a voice channel\n" +
                "2. Use `/join` to bring the bot in\n" +
                "3. Use `/queue add <url>` or `/search <query>` to add a track\n" +
                "Playback starts automatically when the bot is connected and tracks are queued.")
            .AddField("Voice",
                "`/join` — Join your current voice channel\n" +
                "`/leave` — Leave the voice channel")
            .AddField("Queue",
                "`/queue add <url>` — Add a URL or favorite to the queue\n" +
                "`/queue list [page]` — Show the current queue\n" +
                "`/queue resume` — Resume playback\n" +
                "`/queue pause` — Pause playback\n" +
                "`/queue skip [count]` — Skip one or more tracks\n" +
                "`/queue shuffle` — Shuffle the queue\n" +
                "`/queue clear` — Clear all items from the queue\n" +
                "`/queue autoplay [queue_size]` — Toggle autoplay (proactively fills the queue with related tracks from YouTube/Spotify)")
            .AddField("Favorites",
                "`/fav add <url> [alias]` — Save a track or playlist\n" +
                "`/fav list [page]` — Show your favorites\n" +
                "`/fav remove <favorite>` — Remove a favorite\n" +
                "`/fav rename <favorite> <new_name>` — Rename a favorite")
            .AddField("Search",
                "`/search <query>` — Search YouTube and pick a track or playlist to enqueue")
            .AddField("Playlists",
                "`/playlist create <name>` — Create a new empty playlist\n" +
                "`/playlist save <name>` — Save the current queue as a playlist\n" +
                "`/playlist addtrack <playlist>` — Add the currently playing track to a playlist\n" +
                "`/playlist list [page]` — Show your playlists\n" +
                "`/playlist view <playlist> [page]` — View tracks in a playlist\n" +
                "`/playlist rename <playlist> <new_name>` — Rename a playlist\n" +
                "`/playlist delete <playlist>` — Delete a playlist")
            .AddField("History",
                "`/history list [page]` — Show recently played tracks\n" +
                "`/history play <track>` — Search and re-add a track from history to the queue")
            .AddField("Other",
                "`/now` — Show the currently playing track\n" +
                "`/help` — Show this help message")
            .Build();
    }
}
