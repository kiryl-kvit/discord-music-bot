using Discord;
using Discord.Interactions;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Core.MusicSource;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using DiscordMusicBot.Core.MusicSource.Search.Abstraction;
using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App.Modules;

public sealed class SearchModule(
    [FromKeyedServices(SourceType.YouTube)] ISearchProvider searchProvider,
    IUrlProcessorFactory urlProcessorFactory,
    QueuePlaybackService queuePlaybackService,
    ILogger<SearchModule> logger) : InteractionModuleBase
{
    [SlashCommand("search", "Search YouTube and pick a track or playlist to enqueue")]
    public async Task SearchAsync(string query)
    {
        var userId = Context.User.Id;
        var guildId = Context.Guild.Id;

        logger.LogInformation("User {UserId} is searching for \"{Query}\" in guild {GuildId}", userId, query, guildId);
        await DeferAsync(ephemeral: true);

        var results = await searchProvider.SearchAsync(query, SearchEmbedBuilder.MaxResults);

        if (results.Count == 0)
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Content = null;
                props.Embed = ErrorEmbedBuilder.Build("No Results",
                    $"No results found for **{query}**.",
                    "Try a different search query.");
            });
            return;
        }

        var embed = SearchEmbedBuilder.BuildResultsEmbed(query, results);
        var components = SearchEmbedBuilder.BuildSelectMenu(results);

        await ModifyOriginalResponseAsync(props =>
        {
            props.Content = null;
            props.Embed = embed;
            props.Components = components;
        });
    }

    [ComponentInteraction(SearchEmbedBuilder.SelectMenuId)]
    public async Task HandleSelectAsync(string[] selectedValues)
    {
        var url = selectedValues[0];
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;

        logger.LogInformation("User {UserId} selected search result {Url} in guild {GuildId}", userId, url, guildId);
        await DeferAsync(ephemeral: true);

        var urlProcessor = urlProcessorFactory.GetProcessor(url);
        var musicItemsResult = await urlProcessor.GetMusicItemsAsync(url);

        if (!musicItemsResult.IsSuccess || musicItemsResult.Value?.Items.Count == 0)
        {
            await ModifyOriginalResponseAsync(props =>
            {
                props.Content = null;
                props.Embed = ErrorEmbedBuilder.Build("Failed to Process Track",
                    musicItemsResult.ErrorMessage ?? "An unknown error occurred.");
            });
            return;
        }

        var queueItems = musicItemsResult.Value!.Items
            .Select(x => PlayQueueItem.Create(guildId, userId, x.SourceType, x.Url, x.Title, x.Author, x.Duration, x.ThumbnailUrl))
            .ToArray();

        await queuePlaybackService.EnqueueItemsAsync(guildId, queueItems, Context.Channel);

        logger.LogInformation("User {UserId} enqueued search result {Url} in guild {GuildId}", userId, url, guildId);

        var embed = QueueEmbedBuilder.BuildAddedToQueueEmbed(queueItems);
        var disabledMenu = SearchEmbedBuilder.BuildDisabledSelectMenu(queueItems[0].Title);

        await ((IComponentInteraction)Context.Interaction).ModifyOriginalResponseAsync(props =>
        {
            props.Content = null;
            props.Embed = embed;
            props.Components = disabledMenu;
        });
    }
}
