using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordMusicBot.App.Services.Common;
using DiscordMusicBot.App.Services.Favorites;
using DiscordMusicBot.App.Services.NowPlaying;
using DiscordMusicBot.App.Services.Queue;
using DiscordMusicBot.App.Services.Voice;

namespace DiscordMusicBot.App.Modules.NowPlaying;

public sealed class NowPlayingControlsModule(
    QueuePlaybackService queuePlaybackService,
    NowPlayingMessageService nowPlayingMessageService,
    VoiceConnectionService voiceConnectionService,
    FavoriteService favoriteService) : InteractionModuleBase
{
    [SlashCommand("now", "Show the currently playing track")]
    public async Task NowPlayingAsync()
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

    [ComponentInteraction("np:pause_resume")]
    public async Task PauseResumeAsync()
    {
        var guildId = Context.Guild.Id;

        if (!await ValidateVoiceChannelAsync())
        {
            return;
        }

        var isPlaying = queuePlaybackService.IsPlaying(guildId);

        await DeferAsync();

        if (isPlaying)
        {
            await queuePlaybackService.PauseAsync(guildId);
        }
        else
        {
            var result = await queuePlaybackService.StartAsync(guildId);
            if (!result.IsSuccess)
            {
                await FollowupAsync(
                    embed: ErrorEmbedBuilder.Build("Cannot resume", result.ErrorMessage!),
                    ephemeral: true);
                return;
            }
        }
    }

    [ComponentInteraction("np:skip")]
    public async Task SkipAsync()
    {
        var guildId = Context.Guild.Id;

        if (!await ValidateVoiceChannelAsync())
        {
            return;
        }

        if (!queuePlaybackService.IsPlaying(guildId))
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Nothing playing", "There is no track to skip."),
                ephemeral: true);
            return;
        }

        await DeferAsync();

        await queuePlaybackService.SkipAsync(guildId);
    }

    [ComponentInteraction("np:favorite")]
    public async Task FavoriteAsync()
    {
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;

        var currentItem = queuePlaybackService.GetCurrentItem(guildId);
        if (currentItem is null)
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Nothing playing", "There is no track to favorite."),
                ephemeral: true);
            return;
        }

        var result = await favoriteService.AddAsync(
            userId, currentItem.Url, currentItem.Title, alias: null, currentItem.Author,
            currentItem.Duration, isPlaylist: false, currentItem.ThumbnailUrl);

        if (!result.IsSuccess)
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Cannot Add Favorite", result.ErrorMessage!),
                ephemeral: true);
            return;
        }

        var embed = FavoriteEmbedBuilder.BuildAddedEmbed(result.Value!);

        await RespondAsync(embed: embed, ephemeral: true);
    }

    [ComponentInteraction("np:shuffle")]
    public async Task ShuffleAsync()
    {
        var guildId = Context.Guild.Id;

        if (!await ValidateVoiceChannelAsync())
        {
            return;
        }

        var result = await queuePlaybackService.ShuffleQueueAsync(guildId);
        if (!result.IsSuccess)
        {
            await RespondAsync(
                embed: ErrorEmbedBuilder.Build("Cannot shuffle", result.ErrorMessage!),
                ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("Queue Shuffled")
            .WithColor(Color.Green)
            .WithDescription("The queue has been shuffled.")
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    private async Task<bool> ValidateVoiceChannelAsync()
    {
        if (IsUserInBotVoiceChannel())
        {
            return true;
        }

        await RespondAsync(
            embed: ErrorEmbedBuilder.Build("Not in voice channel",
                "You must be in the same voice channel as the bot to use playback controls."),
            ephemeral: true);
        return false;
    }

    private bool IsUserInBotVoiceChannel()
    {
        var guildId = Context.Guild.Id;
        var botVoiceChannelId = voiceConnectionService.GetVoiceChannelId(guildId);

        if (botVoiceChannelId is null)
        {
            return false;
        }

        var guildUser = Context.User as SocketGuildUser;
        return guildUser?.VoiceChannel?.Id == botVoiceChannelId;
    }
}
