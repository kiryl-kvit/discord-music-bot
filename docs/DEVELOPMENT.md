# Development Guide

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

### 3. Run locally

```bash
dotnet run --project DiscordMusicBot.App
```

## Docker

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

## Project Structure

```
DiscordMusicBot.App/          # Startup, Discord interactions, slash commands
DiscordMusicBot.Core/         # Business logic, audio streaming, URL processing
DiscordMusicBot.Domain/       # Domain models
DiscordMusicBot.Infrastructure/ # SQLite persistence, migrations, repositories
```
