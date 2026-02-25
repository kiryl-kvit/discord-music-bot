namespace DiscordMusicBot.Core.Constants;

public static class SupportedSources
{
    public const string YoutubeKey = "youtube";
    public const string SpotifyKey = "spotify";
    public const string SunoKey = "suno";

    private sealed record SourceDefinition(string Key, string Name, string[] Hosts, string[] ExampleHosts);

    private static readonly Dictionary<string, SourceDefinition> AllSources = new(StringComparer.OrdinalIgnoreCase)
    {
        [YoutubeKey] = new SourceDefinition(Key: YoutubeKey,
            Name: "YouTube",
            Hosts: ["youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com", "youtu.be", "www.youtu.be"],
            ExampleHosts: ["youtube.com", "music.youtube.com", "youtu.be"]),

        [SpotifyKey] = new SourceDefinition(Key: SpotifyKey,
            Name: "Spotify",
            Hosts: ["open.spotify.com"],
            ExampleHosts: ["open.spotify.com"]),

        [SunoKey] = new SourceDefinition(Key: SunoKey,
            Name: "Suno",
            Hosts: ["suno.com", "www.suno.com"],
            ExampleHosts: ["suno.com"]),
    };

    private static readonly List<SourceDefinition> ActiveSources = [AllSources[YoutubeKey]];

    public static void Register(string key)
    {
        if (!AllSources.TryGetValue(key, out var source))
        {
            throw new ArgumentException($"Unknown source key '{key}'.", nameof(key));
        }

        if (ActiveSources.Any(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        ActiveSources.Add(source);
    }

    public static bool IsSupported(string url)
    {
        return TryGetSourceKey(url, out _);
    }

    public static bool TryGetSourceKey(string url, out string key)
    {
        key = string.Empty;

        if (!TryCreateSupportedUri(url, out var uri))
        {
            return false;
        }

        foreach (var source in ActiveSources.Where(source =>
                     source.Hosts.Any(host => string.Equals(uri?.Host, host, StringComparison.OrdinalIgnoreCase))))
        {
            key = source.Key;
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