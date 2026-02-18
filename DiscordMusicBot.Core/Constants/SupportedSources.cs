namespace DiscordMusicBot.Core.Constants;

public static class SupportedSources
{
    public const string YoutubeKey = "youtube";

    private sealed record SourceDefinition(string Key, string Name, string[] Hosts, string[] ExampleHosts);

    private static readonly SourceDefinition[] Sources =
    [
        new(Key: YoutubeKey,
            Name: "YouTube",
            Hosts: ["youtube.com", "www.youtube.com", "m.youtube.com", "youtu.be", "www.youtu.be"],
            ExampleHosts: ["youtube.com", "youtu.be"]),
    ];

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

        foreach (var source in Sources)
        {
            if (!source.Hosts.Any(host => string.Equals(uri?.Host, host, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            key = source.Key;
            return true;
        }

        return false;
    }

    public static string GetSupportedSourcesMessage()
    {
        var descriptions = Sources
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