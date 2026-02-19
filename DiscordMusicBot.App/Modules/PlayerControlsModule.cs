using Discord;
using Discord.Interactions;
using DiscordMusicBot.App.Services;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Modules;

public class PlayerControlsModule(
    QueuePlaybackService playbackService,
    NowPlayingService nowPlayingService,
    ILogger<PlayerControlsModule> logger) : InteractionModuleBase
{
    [ComponentInteraction("player:skip")]
    public async Task HandleSkipAsync()
    {
        var guildId = Context.Guild.Id;

        if (!playbackService.IsPlaying(guildId))
        {
            await RespondAsync("Nothing is playing.", ephemeral: true);
            return;
        }

        await ((IComponentInteraction)Context.Interaction).DeferAsync();

        logger.LogInformation("Skip button pressed in guild {GuildId} by user {UserId}",
            guildId, Context.User.Id);

        playbackService.Skip(guildId);
    }

    [ComponentInteraction("player:stop")]
    public async Task HandleStopAsync()
    {
        var guildId = Context.Guild.Id;

        if (!playbackService.IsPlaying(guildId))
        {
            await RespondAsync("Nothing is playing.", ephemeral: true);
            return;
        }

        await ((IComponentInteraction)Context.Interaction).DeferAsync();

        logger.LogInformation("Stop button pressed in guild {GuildId} by user {UserId}",
            guildId, Context.User.Id);

        await playbackService.StopAsync(guildId);
    }

    [ComponentInteraction("player:start")]
    public async Task HandleStartAsync()
    {
        var guildId = Context.Guild.Id;

        if (playbackService.IsPlaying(guildId))
        {
            await RespondAsync("Queue is already playing.", ephemeral: true);
            return;
        }

        await ((IComponentInteraction)Context.Interaction).DeferAsync();

        logger.LogInformation("Start button pressed in guild {GuildId} by user {UserId}",
            guildId, Context.User.Id);

        await playbackService.StartAsync(guildId);

        await nowPlayingService.RefreshAsync(guildId);
    }
}
