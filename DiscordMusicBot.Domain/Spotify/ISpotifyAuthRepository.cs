namespace DiscordMusicBot.Domain.Spotify;

public interface ISpotifyAuthRepository
{
    Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default);
    Task SaveRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
