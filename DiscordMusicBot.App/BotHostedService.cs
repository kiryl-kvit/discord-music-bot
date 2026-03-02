using Discord;
using Discord.WebSocket;
using DiscordMusicBot.App.Options;
using DiscordMusicBot.App.Services.NowPlaying;
using DiscordMusicBot.App.Services.Queue;
using DiscordMusicBot.App.Services.Voice;
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
    NowPlayingMessageService nowPlayingMessageService,
    DatabaseMigrator migrator,
    IOptions<BotSettings> botSettings,
    ILogger<BotHostedService> logger) : IHostedService
{
    private bool _restoreComplete;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() => migrator.Migrate(), cancellationToken);

        discordClient.UserVoiceStateUpdated += voiceConnectionService.HandleVoiceStateUpdated;

        voiceConnectionService.Connected += queuePlaybackService.OnVoiceConnected;
        voiceConnectionService.Disconnected += queuePlaybackService.OnVoiceDisconnected;
        voiceConnectionService.Reconnecting += queuePlaybackService.OnVoiceReconnecting;

        queuePlaybackService.TrackStarted += nowPlayingMessageService.OnTrackStartedAsync;
        queuePlaybackService.TrackLoading += nowPlayingMessageService.OnTrackLoadingAsync;
        queuePlaybackService.PlaybackPaused += nowPlayingMessageService.OnPlaybackPausedAsync;
        queuePlaybackService.PlaybackResumed += nowPlayingMessageService.OnPlaybackResumedAsync;
        queuePlaybackService.PlaybackStopped += nowPlayingMessageService.OnPlaybackStoppedAsync;

        discordClient.Ready += OnReadyAsync;

        await interactionHandler.InitializeAsync(cancellationToken);

        await discordClient.LoginAsync(TokenType.Bot, botSettings.Value.BotToken);
        await discordClient.StartAsync();

        logger.LogInformation("Bot started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Bot is shutting down");

        await nowPlayingMessageService.StopAllAsync();
        await queuePlaybackService.GracefulStopAsync(cancellationToken);
        await voiceConnectionService.DisconnectAllAsync(cancellationToken);
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
