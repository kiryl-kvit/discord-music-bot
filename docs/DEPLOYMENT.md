# Deployment Guide

## 1. Create a Discord Application

Follow the official guide: [Setting up a bot application](https://discord.com/developers/docs/quick-start/getting-started#step-1-creating-an-app).

From the application page, copy:
- **Application ID** -> `APP_ID`
- **Public Key** -> `PUBLIC_KEY`

## 2. Get the Bot Token

On the **Bot** tab, copy (or reset) the token:
- **Token** -> `BOT_TOKEN`

## 3. Configure Privileged Gateway Intents

No privileged intents are required. The bot only uses unprivileged intents (`Guilds`, `GuildVoiceStates`), so leave all privileged intent toggles **disabled**.

## 4. Invite the Bot to Your Server

On the **Installation** tab, generate an invite URL with:

**Scopes:** `bot`, `applications.commands`

**Bot Permissions:** Connect, Speak, Send Messages, Embed Links, Use Voice Activity, Use Slash Commands

## 5. Configure Spotify (optional)

Add to your `.env` file:

```env
SPOTIFY_CLIENT_ID=your_spotify_client_id
SPOTIFY_CLIENT_SECRET=your_spotify_client_secret
```

To obtain credentials: create an app on the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard), select **Web API**, and copy the Client ID and Client Secret. No user login is needed -- the bot uses the [Client Credentials](https://developer.spotify.com/documentation/web-api/tutorials/client-credentials-flow) flow.

If not configured, the bot starts normally with YouTube-only support.

## 6. Configure Suno (optional)

Add to your `.env` file:

```env
SUNO_ENABLED=true
```

No API credentials required. If not set, Suno links are treated as unsupported.

## 7. Configure Database Path (optional)

The bot uses SQLite for persistent queue storage. By default, the database is stored at `database.db` in the working directory. To customize:

```env
DATABASE_PATH=/app/data/database.db
```

In Docker, the database defaults to `/app/data/database.db`. Mount a volume to `/app/data` to persist data across restarts (see Docker Compose below).

## Docker

### Build locally

```bash
docker build -t discord-music-bot .
docker run -d --env-file .env discord-music-bot
```

### Pull from GitHub Container Registry

```bash
docker pull ghcr.io/kiryl-kvit/discord-music-bot:latest
docker run -d --env-file .env ghcr.io/kiryl-kvit/discord-music-bot:latest
```

### Docker Compose

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

```bash
docker compose up -d
```

See the [environment variables reference](../README.md#environment-variables) for all `.env` options.
