namespace DiscordMusicBot.Core.MusicSource.Related;

public interface IRelatedTrackProvider
{
    Task<IReadOnlyList<MusicSource>> GetRelatedTracksAsync(string seedVideoUrl, IReadOnlyList<string> excludeUrls,
        int count, CancellationToken cancellationToken = default);
}
