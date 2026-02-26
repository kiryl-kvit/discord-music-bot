# Discord Music Bot

A Discord music bot built with .NET 10 and [Discord.Net](https://github.com/discord-net/Discord.Net).

## Supported Sources

| Source        | Supported Links           | Setup                                        |
|---------------|---------------------------|----------------------------------------------|
| YouTube       | Single videos, playlists  | Built-in, no configuration needed            |
| YouTube Music | Single videos, playlists  | Built-in, no configuration needed            |
| Spotify       | Tracks, playlists, albums | Requires `SPOTIFY_CLIENT_ID` and `SPOTIFY_CLIENT_SECRET` ([details](docs/DEPLOYMENT.md#5-configure-spotify-optional)) |
| Suno          | Songs, playlists          | Requires `SUNO_ENABLED=true` ([details](docs/DEPLOYMENT.md#6-configure-suno-optional)) |

Spotify resolves tracks to YouTube for audio playback. Suno streams audio directly from its CDN.

## Documentation

- **[Development Guide](docs/DEVELOPMENT.md)** -- local setup, project structure
- **[Deployment Guide](docs/DEPLOYMENT.md)** -- Discord app setup, optional integrations, Docker

## Commands

| Command | Description |
|---------|-------------|
| `/queue add <url>` | Enqueue a track, playlist, or favorite |
| `/queue list` | Show the current queue |
| `/queue skip [count]` | Skip one or more tracks |
| `/queue shuffle` | Shuffle the queue |
| `/queue pause` | Pause playback |
| `/queue resume` | Resume playback |
| `/queue clear` | Clear all items from the queue |
| `/fav add <url> [alias]` | Save a track or playlist to your favorites |
| `/fav list [page]` | Show your saved favorites |
| `/fav remove <favorite>` | Remove a favorite (supports autocomplete) |
| `/join` | Join your voice channel |
| `/leave` | Leave the voice channel |
| `/help` | Show available commands |

## Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `BOT_TOKEN` | Yes | -- | Discord bot token |
| `APP_ID` | Yes | -- | Discord application ID |
| `PUBLIC_KEY` | Yes | -- | Discord application public key |
| `PLAYLIST_LIMIT` | No | `50` | Max tracks loaded from a playlist (`0` = unlimited) |
| `VOLUME` | No | `1.0` | Playback volume (`0.0` - `1.0`) |
| `SPOTIFY_CLIENT_ID` | No | -- | Spotify app client ID |
| `SPOTIFY_CLIENT_SECRET` | No | -- | Spotify app client secret |
| `SUNO_ENABLED` | No | `false` | Enable Suno source support |
| `FAVORITES_LIMIT` | No | `100` | Max favorites per user (`0` = unlimited) |
| `DATABASE_PATH` | No | `database.db` | Path to the SQLite database file |

## Third-Party Licenses

This project uses the following open-source libraries (all MIT-licensed):
[Discord.Net](https://github.com/discord-net/Discord.Net),
[YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode),
[FFMpegCore](https://github.com/rosenbjerg/FFMpegCore),
[DotNetEnv](https://github.com/tonerdo/dotnet-env),
[SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET).

Pre-built Docker images include [FFmpeg](https://packages.debian.org/source/ffmpeg) (GPL v2+). This project's own source code remains under the MIT License.

## License

MIT License. See [LICENSE](LICENSE) for details.
