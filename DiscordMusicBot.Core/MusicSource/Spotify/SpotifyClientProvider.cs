using DiscordMusicBot.Core.MusicSource.Options;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace DiscordMusicBot.Core.MusicSource.Spotify;

public sealed class SpotifyClientProvider(IOptionsMonitor<SpotifyOptions> options)
{
    private readonly Lock _lock = new();
    private SpotifyClient? _client;
    private string? _currentClientId;
    private string? _currentClientSecret;
    private string? _currentRefreshToken;

    public void SetRefreshToken(string refreshToken)
    {
        lock (_lock)
        {
            if (string.Equals(_currentRefreshToken, refreshToken, StringComparison.Ordinal))
                return;

            _currentRefreshToken = refreshToken;
            _client = null;
        }
    }

    public SpotifyClient GetClient()
    {
        var opts = options.CurrentValue;

        lock (_lock)
        {
            if (_client is not null &&
                string.Equals(_currentClientId, opts.ClientId, StringComparison.Ordinal) &&
                string.Equals(_currentClientSecret, opts.ClientSecret, StringComparison.Ordinal))
            {
                return _client;
            }

            SpotifyClientConfig config;

            if (!string.IsNullOrEmpty(_currentRefreshToken))
            {
                var initialResponse = new AuthorizationCodeTokenResponse
                {
                    RefreshToken = _currentRefreshToken
                };

                config = SpotifyClientConfig
                    .CreateDefault()
                    .WithAuthenticator(
                        new AuthorizationCodeAuthenticator(opts.ClientId, opts.ClientSecret, initialResponse));
            }
            else
            {
                config = SpotifyClientConfig
                    .CreateDefault()
                    .WithAuthenticator(new ClientCredentialsAuthenticator(opts.ClientId, opts.ClientSecret));
            }

            _client = new SpotifyClient(config);
            _currentClientId = opts.ClientId;
            _currentClientSecret = opts.ClientSecret;

            return _client;
        }
    }
}
