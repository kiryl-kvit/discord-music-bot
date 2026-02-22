using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordMusicBot.App.Options;
using DiscordMusicBot.App.Services;
using DiscordMusicBot.Core.Constants;
using DiscordMusicBot.Core.MusicSource.Options;
using DiscordMusicBot.Core.MusicSource.AudioStreaming;
using DiscordMusicBot.Core.MusicSource.AudioStreaming.Abstraction;
using DiscordMusicBot.Core.MusicSource.Processors;
using DiscordMusicBot.Core.MusicSource.Processors.Abstraction;
using DiscordMusicBot.Core.MusicSource.Spotify;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YoutubeExplode;

namespace DiscordMusicBot.App;

public static class ServicesConfiguration
{
    public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureBotOptions(configuration)
            .ConfigureDiscordDotNet();

        services.AddKeyedScoped<IUrlProcessor, YoutubeUrlProcessor>(SupportedSources.YoutubeKey);
        services.AddScoped<IUrlProcessorFactory, UrlProcessorFactory>();
        services.AddSingleton<YoutubeClient>();

        services.AddKeyedSingleton<IAudioStreamProvider, YoutubeAudioStreamProvider>(SupportedSources.YoutubeKey);
        services.AddSingleton<IAudioStreamProviderFactory, AudioStreamProviderFactory>();

        services.ConfigureSpotify(configuration);

        return services;
    }

    extension(IServiceCollection services)
    {
        private IServiceCollection ConfigureBotOptions(IConfiguration configuration)
        {
            services.BindOptions<BotSettings>(configuration, BotSettings.SectionName)
                .Validate(o => !string.IsNullOrWhiteSpace(o.BotToken), "BotSettings:BotToken is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.AppId), "BotSettings:AppId is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.PublicKey), "BotSettings:PublicKey is required.")
                .ValidateOnStart();

            services.BindOptions<MusicSourcesOptions>(configuration, MusicSourcesOptions.SectionName)
                .Validate(o => o.PlaylistLimit > 0, "MusicSources:PlaylistLimit must be greater than 0.")
                .Validate(o => o.Volume is >= 0.0 and <= 2.0,
                    "MusicSources:Volume must be between 0.0 and 2.0.")
                .ValidateOnStart();

            return services;
        }

        private IServiceCollection ConfigureDiscordDotNet()
        {
            var socketConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildVoiceStates |
                                 GatewayIntents.Guilds,
                LogLevel = LogSeverity.Info,
            };

            services.AddSingleton(socketConfig);
            services.AddSingleton<DiscordSocketClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DiscordSocketClient>>();
                var client = new DiscordSocketClient(socketConfig);

                client.Log += Logging.CreateLogHandler(logger);

                return client;
            });

            services.AddSingleton<InteractionService>(sp =>
            {
                var client = sp.GetRequiredService<DiscordSocketClient>();
                var logger = sp.GetRequiredService<ILogger<InteractionService>>();

                var interactionService = new InteractionService(client);

                interactionService.Log += Logging.CreateLogHandler(logger);

                return interactionService;
            });

            services.AddSingleton<InteractionHandler>();

            services.AddSingleton<VoiceConnectionService>();
            services.AddSingleton<QueuePlaybackService>();

            return services;
        }

        private OptionsBuilder<TOptions> BindOptions<TOptions>(IConfiguration configuration, string sectionName)
            where TOptions : class
        {
            return services.AddOptions<TOptions>()
                .Bind(configuration.GetSection(sectionName));
        }

        private IServiceCollection ConfigureSpotify(IConfiguration configuration)
        {
            var section = configuration.GetSection(SpotifyOptions.SectionName);
            var clientId = section[nameof(SpotifyOptions.ClientId)];
            var clientSecret = section[nameof(SpotifyOptions.ClientSecret)];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                return services;
            }

            SupportedSources.Register(SupportedSources.SpotifyKey);

            services.BindOptions<SpotifyOptions>(configuration, SpotifyOptions.SectionName)
                .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), "Spotify:ClientId is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.ClientSecret), "Spotify:ClientSecret is required.")
                .ValidateOnStart();

            services.AddSingleton<SpotifyClientProvider>();
            services.AddKeyedScoped<IUrlProcessor, SpotifyUrlProcessor>(SupportedSources.SpotifyKey);

            return services;
        }
    }
}
