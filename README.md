# Discord Music Bot

A Discord music bot built with .NET 10 and [Discord.Net](https://github.com/discord-net/Discord.Net)

## Supported Sources

| Source        | Supported Links           | Required Config                              |
|---------------|---------------------------|----------------------------------------------|
| YouTube       | Single videos, playlists  | None (built-in)                              |
| YouTube Music | Single videos, playlists  | None (built-in)                              |
| Spotify       | Tracks, playlists, albums | `SPOTIFY_CLIENT_ID`, `SPOTIFY_CLIENT_SECRET` |
| Suno          | Songs, playlists          | `SUNO_ENABLED=true`                          |

**YouTube** and **YouTube Music** are always available out of the box.

**Spotify** support is optional. When configured, the bot fetches track metadata from the Spotify API and resolves each track to YouTube for audio playback. If Spotify credentials are not provided, Spotify links are treated as unsupported.

**Suno** support is optional. When enabled, the bot scrapes song metadata from suno.com pages and streams audio directly from Suno's CDN. No API credentials are required -- just set `SUNO_ENABLED=true` to activate it.

## Documentation

- **[Development Guide](docs/DEVELOPMENT.md)**
- **[Deployment Guide](docs/DEPLOYMENT.md)**

## Third-Party Licenses

This project uses the following open-source libraries, all licensed under MIT:

- [Discord.Net](https://github.com/discord-net/Discord.Net) - Discord API client
- [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) - YouTube data extraction
- [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) - FFmpeg .NET wrapper
- [DotNetEnv](https://github.com/tonerdo/dotnet-env) - .env file loader
- [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET) - Spotify Web API client

Pre-built Docker images include FFmpeg, which is licensed under GPL v2+. The FFmpeg source code is available from the Debian package repository (https://packages.debian.org/source/ffmpeg). Your use of the Docker image is subject to FFmpeg's license terms. This project's own source code remains under the MIT License.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
