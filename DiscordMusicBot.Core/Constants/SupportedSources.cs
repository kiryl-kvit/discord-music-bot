using DiscordMusicBot.Core.MusicSource;

namespace DiscordMusicBot.Core.Constants;

public static class SupportedSources
{
    private sealed record SourceDefinition(SourceType SourceType, string Name, string[] Hosts, string[] ExampleHosts);

    private static readonly Dictionary<SourceType, SourceDefinition> AllSources = new()
    {
        [SourceType.YouTube] = new SourceDefinition(SourceType: SourceType.YouTube,
            Name: "YouTube",
            Hosts: ["youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com", "youtu.be", "www.youtu.be"],
            ExampleHosts: ["youtube.com", "music.youtube.com", "youtu.be"]),

        [SourceType.Spotify] = new SourceDefinition(SourceType: SourceType.Spotify,
            Name: "Spotify",
            Hosts: ["open.spotify.com"],
            ExampleHosts: ["open.spotify.com"]),

        [SourceType.Suno] = new SourceDefinition(SourceType: SourceType.Suno,
            Name: "Suno",
            Hosts: ["suno.com", "www.suno.com"],
            ExampleHosts: ["suno.com"]),
    };

    private static readonly List<SourceDefinition> ActiveSources = [AllSources[SourceType.YouTube]];

    public static void Register(SourceType sourceType)
    {
        if (!AllSources.TryGetValue(sourceType, out var source))
        {
            throw new ArgumentException($"Unknown source type '{sourceType}'.", nameof(sourceType));
        }

        if (ActiveSources.Any(s => s.SourceType == sourceType))
        {
            return;
        }

        ActiveSources.Add(source);
    }

    public static bool IsSupported(string url)
    {
        return TryGetSourceType(url, out _);
    }

    public static bool TryGetSourceType(string url, out SourceType sourceType)
    {
        sourceType = default;

        if (!TryCreateSupportedUri(url, out var uri))
        {
            return false;
        }

        foreach (var source in ActiveSources.Where(source =>
                     source.Hosts.Any(host => string.Equals(uri?.Host, host, StringComparison.OrdinalIgnoreCase))))
        {
            sourceType = source.SourceType;
            return true;
        }

        return false;
    }

    public static string GetSupportedSourcesMessage()
    {
        var descriptions = ActiveSources
            .Select(source => $"{source.Name} ({string.Join(", ", source.ExampleHosts)})")
            .ToArray();

        return $"Supported sources: {string.Join(", ", descriptions)}.";
    }

    private static bool TryCreateSupportedUri(string url, out Uri? uri)
    {
        uri = null!;

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    }
}
