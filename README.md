# Discord Music Bot

A Discord music bot built with .NET 10 and [Discord.Net](https://github.com/discord-net/Discord.Net)

## Features

- Play music from YouTube, YouTube Music, and Spotify links (tracks, playlists, and albums)
- Queue management with slash commands

## Supported Sources

| Source        | Supported Links           | Required Config                              |
|---------------|---------------------------|----------------------------------------------|
| YouTube       | Single videos, playlists  | None (built-in)                              |
| YouTube Music | Single videos, playlists  | None (built-in)                              |
| Spotify       | Tracks, playlists, albums | `SPOTIFY_CLIENT_ID`, `SPOTIFY_CLIENT_SECRET` |

**YouTube** and **YouTube Music** are always available out of the box.

**Spotify** support is optional. When configured, the bot fetches track metadata from the Spotify API and resolves each track to YouTube for audio playback. If Spotify credentials are not provided, Spotify links are treated as unsupported.

## Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [FFmpeg](https://ffmpeg.org/download.html) (must be available in `PATH`)
- A Discord bot token ([Discord Developer Portal](https://discord.com/developers/applications))

## Setup

### 1. Clone the repository

```bash
git clone https://github.com/kiryl-kvit/discord-music-bot.git
cd discord-music-bot
```

### 2. Configure environment variables

Create a `.env` file in `DiscordMusicBot.App/`:

```env
BOT_TOKEN=your_bot_token
APP_ID=your_application_id
PUBLIC_KEY=your_public_key
PLAYLIST_LIMIT=50
```

### 3. Configure Spotify (optional)

To enable Spotify support, add the following to your `.env` file:

```env
SPOTIFY_CLIENT_ID=your_spotify_client_id
SPOTIFY_CLIENT_SECRET=your_spotify_client_secret
```

To obtain these credentials:

1. Go to the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
2. Create a new application
   - When prompted for a **Redirect URI**, enter `https://localhost` -- this is required by the form but is not used by the bot
   - Select **Web API** when asked which API/SDKs you plan to use
3. Copy the **Client ID** and **Client Secret** from the application settings

No Spotify user login is required. The bot uses the [Client Credentials](https://developer.spotify.com/documentation/web-api/tutorials/client-credentials-flow) flow, which only requires app-level credentials.

If these variables are not set, the bot starts normally with YouTube-only support.

### 4. Run locally

```bash
dotnet run --project DiscordMusicBot.App
```

### Docker

Build and run with Docker:

```bash
docker build -t discord-music-bot .
docker run -d --env-file DiscordMusicBot.App/.env discord-music-bot
```

Or pull the pre-built image from GitHub Container Registry:

```bash
docker pull ghcr.io/kiryl-kvit/discord-music-bot:latest
docker run -d --env-file .env ghcr.io/kiryl-kvit/discord-music-bot:latest
```

## Deployment

### 1. Create a Discord Application

Follow the official Discord guide to create a new application and bot user:
[Setting up a bot application](https://discord.com/developers/docs/quick-start/getting-started#step-1-creating-an-app)

From the application page, copy the following values for your `.env` file:
- **Application ID** → `APP_ID`
- **Public Key** → `PUBLIC_KEY`

### 2. Get the Bot Token

Navigate to the **Bot** tab and copy (or reset) the token:
- **Token** → `BOT_TOKEN`

### 3. Configure Privileged Gateway Intents

No privileged intents are required. The bot only uses unprivileged intents (`Guilds`, `GuildVoiceStates`, etc.), so you can leave all privileged intent toggles **disabled** on the Bot tab.

### 4. Invite the Bot to Your Server

Go to the **Installation** tab and generate an invite URL with the following settings:

**Scopes:**
- `bot`
- `applications.commands`

**Bot Permissions:**
- Connect
- Speak
- Send Messages
- Embed Links
- Use Voice Activity
- Use Slash Commands
- Bypass Slowmode (Optional, but recommended for better user experience)

### Docker Compose

Create a `docker-compose.yml` file:

```yaml
services:
  discord-music-bot:
    image: ghcr.io/kiryl-kvit/discord-music-bot:latest
    container_name: discord-music-bot
    restart: unless-stopped
    env_file:
      - .env
    network_mode: host
```

Create a `.env` file in the same directory:

```env
BOT_TOKEN=your_bot_token
APP_ID=your_application_id
PUBLIC_KEY=your_public_key
PLAYLIST_LIMIT=50
VOLUME=1.0
SPOTIFY_CLIENT_ID=your_spotify_client_id
SPOTIFY_CLIENT_SECRET=your_spotify_client_secret
```

Then run:

```bash
docker compose up -d
```

## Project Structure

```
DiscordMusicBot.App/          # Startup, Discord interactions, slash commands
DiscordMusicBot.Core/         # Business logic, audio streaming, URL processing
DiscordMusicBot.Domain/       # Domain models
```

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
