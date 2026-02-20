# Discord Music Bot

A Discord music bot built with .NET 10 and [Discord.Net](https://github.com/discord-net/Discord.Net)

## Features

- Play music from YouTube and YouTube Music links (single tracks and playlists)
- Queue management with slash commands

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
DB_FILE_PATH=database.db
PLAYLIST_LIMIT=50
```

### 3. Run locally

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
    volumes:
      - bot-data:/app/data
    network_mode: host

volumes:
  bot-data:
```

Create a `.env` file in the same directory:

```env
BOT_TOKEN=your_bot_token
APP_ID=your_application_id
PUBLIC_KEY=your_public_key
DB_FILE_PATH=/app/data/database.db
PLAYLIST_LIMIT=50
```

> **Note:** `DB_FILE_PATH` must use a path inside the container. The `/app/data` directory is backed by a named volume, so the database persists across container restarts.

Then run:

```bash
docker compose up -d
```

## Project Structure

```
DiscordMusicBot.App/          # Startup, Discord interactions, slash commands
DiscordMusicBot.Core/         # Business logic, audio streaming, URL processing
DiscordMusicBot.Domain/       # Domain interfaces and models
DiscordMusicBot.DataAccess/   # EF Core SQLite persistence
```

## Third-Party Licenses

This project uses the following open-source libraries, all licensed under MIT:

- [Discord.Net](https://github.com/discord-net/Discord.Net) - Discord API client
- [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) - YouTube data extraction
- [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) - FFmpeg .NET wrapper
- [DotNetEnv](https://github.com/tonerdo/dotnet-env) - .env file loader

Pre-built Docker images include FFmpeg, which is licensed under GPL v2+. The FFmpeg source code is available from the Debian package repository (https://packages.debian.org/source/ffmpeg). Your use of the Docker image is subject to FFmpeg's license terms. This project's own source code remains under the MIT License.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
