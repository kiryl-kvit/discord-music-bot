namespace DiscordMusicBot.Domain.Playback;

public sealed class PersistedGuildState
{
    public ulong GuildId { get; }
    public ulong VoiceChannelId { get; }
    public ulong? FeedbackChannelId { get; }
    public TimeSpan ResumePosition { get; }
    public long? ResumeItemId { get; }

    public PersistedGuildState(
        ulong guildId,
        ulong voiceChannelId,
        ulong? feedbackChannelId,
        TimeSpan resumePosition,
        long? resumeItemId)
    {
        GuildId = guildId;
        VoiceChannelId = voiceChannelId;
        FeedbackChannelId = feedbackChannelId;
        ResumePosition = resumePosition;
        ResumeItemId = resumeItemId;
    }
}
