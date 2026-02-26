using Discord;
using Discord.Interactions;
using DiscordMusicBot.App.Services;

namespace DiscordMusicBot.App.Modules;

public sealed class NowPlayingControlsModule(QueuePlaybackService queuePlaybackService) : InteractionModuleBase
{
    [ComponentInteraction("np:pauseresume")]
    public async Task HandlePauseResumeAsync()
    {
        var guildId = Context.Guild.Id;
        var interaction = (IComponentInteraction)Context.Interaction;

        if (queuePlaybackService.IsPlaying(guildId))
        {
            await queuePlaybackService.PauseAsync(guildId);
        }
        else
        {
            await queuePlaybackService.StartAsync(guildId);
        }

        var currentItem = queuePlaybackService.GetCurrentItem(guildId);
        if (currentItem is null)
        {
            await interaction.UpdateAsync(msg =>
            {
                msg.Embed = NowPlayingEmbedBuilder.BuildEmbed(null!, isPaused: true);
                msg.Components = NowPlayingEmbedBuilder.BuildControls(isPaused: true);
            });
            return;
        }

        var isPaused = !queuePlaybackService.IsPlaying(guildId);
        await interaction.UpdateAsync(msg =>
        {
            msg.Embed = NowPlayingEmbedBuilder.BuildEmbed(currentItem, isPaused);
            msg.Components = NowPlayingEmbedBuilder.BuildControls(isPaused);
        });
    }

    [ComponentInteraction("np:skip")]
    public async Task HandleSkipAsync()
    {
        var guildId = Context.Guild.Id;
        var interaction = (IComponentInteraction)Context.Interaction;

        if (!queuePlaybackService.IsPlaying(guildId))
        {
            await interaction.DeferAsync();
            return;
        }

        await queuePlaybackService.SkipAsync(guildId);
        await interaction.DeferAsync();
    }

    [ComponentInteraction("np:shuffle")]
    public async Task HandleShuffleAsync()
    {
        var guildId = Context.Guild.Id;
        var interaction = (IComponentInteraction)Context.Interaction;

        var result = await queuePlaybackService.ShuffleQueueAsync(guildId);

        if (!result.IsSuccess)
        {
            await interaction.DeferAsync();
            return;
        }

        var currentItem = queuePlaybackService.GetCurrentItem(guildId);
        var isPaused = !queuePlaybackService.IsPlaying(guildId);

        if (currentItem is not null)
        {
            await interaction.UpdateAsync(msg =>
            {
                msg.Embed = NowPlayingEmbedBuilder.BuildEmbed(currentItem, isPaused);
                msg.Components = NowPlayingEmbedBuilder.BuildControls(isPaused);
            });
        }
        else
        {
            await interaction.DeferAsync();
        }
    }

    [ComponentInteraction("np:queue")]
    public async Task HandleQueueAsync()
    {
        var guildId = Context.Guild.Id;

        const int pageSize = QueueEmbedBuilder.PageSize;

        var items = await queuePlaybackService.GetQueueItemsAsync(guildId, skip: 0, take: pageSize + 1);
        var currentItem = queuePlaybackService.GetCurrentItem(guildId);
        var stats = await queuePlaybackService.GetQueueStatsAsync(guildId);
        var hasNextPage = items.Count > pageSize;
        var pageItems = hasNextPage ? items.Take(pageSize).ToList() : items;

        var embed = QueueEmbedBuilder.BuildQueueEmbed(pageItems, currentItem, page: 1, pageSize, stats);
        var components = QueueEmbedBuilder.BuildQueuePageControls(page: 1, hasNextPage);

        await RespondAsync(embed: embed, components: components, ephemeral: true);
    }
}
