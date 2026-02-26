using Discord.Audio;

namespace DiscordMusicBot.App.Services.Models;

public sealed record VoiceConnection(IAudioClient Client, ulong ChannelId);
