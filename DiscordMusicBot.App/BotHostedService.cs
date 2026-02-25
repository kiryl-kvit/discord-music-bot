using Discord;
using Discord.WebSocket;
using DiscordMusicBot.App.Options;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Infrastructure.Database;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusicBot.App;

public sealed class BotHostedService(
    DiscordSocketClient discordClient,
    InteractionHandler interactionHandler,
    VoiceConnectionService voiceConnectionService,
    QueuePlaybackService queuePlaybackService,
    DatabaseMigrator migrator,
    IOptions<BotSettings> botSettings,
    ILogger<BotHostedService> logger) : IHostedService
{
    private bool _restoreComplete;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        migrator.Migrate();

        discordClient.UserVoiceStateUpdated += voiceConnectionService.HandleVoiceStateUpdated;

        voiceConnectionService.Connected += queuePlaybackService.OnVoiceConnected;
        voiceConnectionService.Disconnected += queuePlaybackService.OnVoiceDisconnected;

        discordClient.Ready += OnReadyAsync;

        await interactionHandler.InitializeAsync();

        await discordClient.LoginAsync(TokenType.Bot, botSettings.Value.BotToken);
        await discordClient.StartAsync();

        logger.LogInformation("Bot started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Bot is shutting down");

        await queuePlaybackService.GracefulStopAsync();
        await voiceConnectionService.DisconnectAllAsync();
        await discordClient.StopAsync();

        logger.LogInformation("Bot stopped");
    }

    private async Task OnReadyAsync()
    {
        if (_restoreComplete)
        {
            return;
        }

        _restoreComplete = true;
        await queuePlaybackService.RestoreAsync();
    }
}
