using DiscordMusicBot.Domain.Playback;

namespace DiscordMusicBot.Infrastructure.Dto;

internal sealed class GuildPlaybackStateRow
{
    public string GuildId { get; init; } = null!;
    public string VoiceChannelId { get; init; } = null!;
    public string? FeedbackChannelId { get; init; }
    public long ResumePositionMs { get; init; }
    public long? ResumeItemId { get; init; }

    public PersistedGuildState ToPersistedGuildState()
    {
        return new PersistedGuildState(
            ulong.Parse(GuildId),
            ulong.Parse(VoiceChannelId),
            FeedbackChannelId is not null ? ulong.Parse(FeedbackChannelId) : null,
            TimeSpan.FromMilliseconds(ResumePositionMs),
            ResumeItemId);
    }
}
