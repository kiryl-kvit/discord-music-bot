namespace DiscordMusicBot.Core.MusicSource.Related;

public interface IRelatedTrackProvider
{
    Task<MusicSource?> GetRelatedTrackAsync(string seedVideoUrl, IReadOnlyList<string> excludeUrls,
        CancellationToken cancellationToken = default);
}
