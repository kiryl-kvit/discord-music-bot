using Discord.Audio;

namespace DiscordMusicBot.App.Services.Voice;

public sealed record VoiceConnection(IAudioClient Client, ulong ChannelId);
