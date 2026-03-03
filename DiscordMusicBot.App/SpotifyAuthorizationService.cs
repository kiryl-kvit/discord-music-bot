using System.Net;
using System.Text;
using DiscordMusicBot.Core.MusicSource.Options;
using DiscordMusicBot.Core.MusicSource.Spotify;
using DiscordMusicBot.Domain.Spotify;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace DiscordMusicBot.App;

public sealed class SpotifyAuthorizationService(
    SpotifyClientProvider clientProvider,
    ISpotifyAuthRepository authRepository,
    IOptionsMonitor<SpotifyOptions> options,
    ILogger<SpotifyAuthorizationService> logger) : BackgroundService
{
    private const string CallbackUrl = "http://127.0.0.1:5543/callback";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var refreshToken = await authRepository.GetRefreshTokenAsync(stoppingToken);

        if (refreshToken != null)
        {
            clientProvider.SetRefreshToken(refreshToken);
            logger.LogInformation("Spotify: loaded stored authorization token.");
            return;
        }

        logger.LogInformation("Spotify: no authorization token found — starting OAuth flow.");

        await RunOAuthFlowAsync(stoppingToken);
    }

    private async Task RunOAuthFlowAsync(CancellationToken stoppingToken)
    {
        var opts = options.CurrentValue;
        var state = Guid.NewGuid().ToString("N");

        var loginRequest = new LoginRequest(
            new Uri(CallbackUrl),
            opts.ClientId,
            LoginRequest.ResponseType.Code)
        {
            Scope = [Scopes.PlaylistReadPrivate, Scopes.PlaylistReadCollaborative],
            State = state
        };

        var authUrl = loginRequest.ToUri();

        using var listener = new HttpListener();
        listener.Prefixes.Add(CallbackUrl + "/");

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            logger.LogError(ex,
                "Spotify: could not start OAuth callback listener on {Url}. " +
                "Ensure port 5543 is not in use and the application has permission to listen on it.",
                CallbackUrl);
            return;
        }

        logger.LogWarning(
            "----------------------------------------------------------------------");
        logger.LogWarning(
            "Spotify authorization required.");
        logger.LogWarning(
            "Before proceeding, make sure '{CallbackUrl}' is added as a Redirect URI " +
            "in your Spotify app settings at https://developer.spotify.com/dashboard",
            CallbackUrl);
        logger.LogWarning(
            "Open this URL in your browser to authorize the bot:");
        logger.LogWarning(
            "{AuthUrl}", authUrl);
        logger.LogWarning(
            "Running on a remote server? Forward the port over SSH first:");
        logger.LogWarning(
            "  ssh -L 5543:127.0.0.1:5543 user@your-server");
        logger.LogWarning(
            "Waiting for authorization... (stop the bot with Ctrl+C to cancel)");
        logger.LogWarning(
            "----------------------------------------------------------------------");

        // Register listener.Stop() so GetContextAsync unblocks when the host shuts down.
        // Do NOT stop the listener inside a finally block — that disposes the response
        // before RespondAsync can write to it. The using declaration above handles cleanup
        // when this method returns; the registration handles cancellation.
        await using var _ = stoppingToken.Register(() => listener.Stop());

        HttpListenerContext context;
        try
        {
            context = await listener.GetContextAsync();
        }
        catch (HttpListenerException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Spotify: OAuth flow cancelled.");
            return;
        }
        catch (HttpListenerException ex)
        {
            logger.LogError(ex, "Spotify: OAuth callback listener error.");
            return;
        }

        var query = context.Request.QueryString;
        var callbackState = query["state"];
        var code = query["code"];
        var error = query["error"];

        if (!string.Equals(callbackState, state, StringComparison.Ordinal))
        {
            await RespondAsync(context, success: false,
                message: "Authorization failed: state mismatch. Please try again.");
            logger.LogError("Spotify: OAuth callback state mismatch — possible CSRF attempt.");
            return;
        }

        if (error != null || code == null)
        {
            await RespondAsync(context, success: false,
                message: $"Authorization failed: {error ?? "no code received"}. Please try again.");
            logger.LogError("Spotify: OAuth authorization denied or failed. Error: {Error}", error);
            return;
        }

        AuthorizationCodeTokenResponse tokenResponse;
        try
        {
            var oauthClient = new OAuthClient();
            tokenResponse = await oauthClient.RequestToken(
                new AuthorizationCodeTokenRequest(
                    opts.ClientId,
                    opts.ClientSecret,
                    code,
                    new Uri(CallbackUrl)),
                stoppingToken);
        }
        catch (Exception ex)
        {
            await RespondAsync(context, success: false,
                message: "Authorization failed: could not exchange code for token. Check the bot logs.");
            logger.LogError(ex, "Spotify: token exchange failed.");
            return;
        }

        await RespondAsync(context, success: true,
            message: "Spotify authorization successful. You can close this tab and return to the bot.");

        await authRepository.SaveRefreshTokenAsync(tokenResponse.RefreshToken, stoppingToken);
        clientProvider.SetRefreshToken(tokenResponse.RefreshToken);

        logger.LogInformation("Spotify: authorization complete. Playlist support is now active.");
    }

    private static async Task RespondAsync(HttpListenerContext context, bool success, string message)
    {
        var color = success ? "#4caf50" : "#f44336";
        var html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><title>Spotify Authorization</title></head>
            <body style="font-family:sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;background:#1a1a2e">
              <div style="background:#16213e;color:#e0e0e0;padding:2rem 3rem;border-radius:12px;text-align:center;max-width:480px;border-top:4px solid {color}">
                <h2 style="margin-top:0;color:{color}">{(success ? "Success" : "Error")}</h2>
                <p>{message}</p>
              </div>
            </body>
            </html>
            """;

        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.StatusCode = success ? 200 : 400;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;

        await using var stream = context.Response.OutputStream;
        await stream.WriteAsync(bytes);
    }
}
