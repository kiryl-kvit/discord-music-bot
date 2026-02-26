# Development Guide

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [FFmpeg](https://ffmpeg.org/download.html) (must be in `PATH`)
- A Discord bot token ([Developer Portal](https://discord.com/developers/applications))

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
```

See the [environment variables reference](../README.md#environment-variables) for all available options.

### 3. Run

```bash
dotnet run --project DiscordMusicBot.App
```

Or use the convenience script:

```bash
./start.sh
```

## Project Structure

```
DiscordMusicBot.App/            # Entry point, Discord interactions, slash commands, DI wiring
DiscordMusicBot.Core/           # Business logic: URL processing, audio streaming, music sources
DiscordMusicBot.Domain/         # Domain models and repository interfaces (no external dependencies)
DiscordMusicBot.Infrastructure/ # SQLite persistence with Dapper, migrations, repository implementations
```
