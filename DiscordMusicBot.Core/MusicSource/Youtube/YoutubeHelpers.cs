using YoutubeExplode.Videos;

namespace DiscordMusicBot.Core.MusicSource.Youtube;

public abstract class YoutubeHelpers
{
    public static string VideoUrl(VideoId videoId) => $"https://www.youtube.com/watch?v={videoId}";
}