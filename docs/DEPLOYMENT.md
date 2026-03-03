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

Spotify support requires two `.env` entries and a one-time browser-based authorization.

### 5.1. Create a Spotify app

1. Go to the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard) and create an app (select **Web API**).
2. Copy the **Client ID** and **Client Secret**.
3. Open the app's **Settings**, scroll to **Redirect URIs**, and add:
   ```
   http://127.0.0.1:5543/callback
   ```
   Save the settings. (`localhost` is not accepted by Spotify — use the explicit IPv4 loopback address.)

### 5.2. Add credentials to `.env`

```env
SPOTIFY_CLIENT_ID=your_spotify_client_id
SPOTIFY_CLIENT_SECRET=your_spotify_client_secret
```

### 5.3. Authorize the bot (first run only)

The bot uses the [Authorization Code](https://developer.spotify.com/documentation/web-api/tutorials/code-flow) flow so it can access playlists. On the **first start** after credentials are configured, the bot will print a message like this to the logs:

```
Spotify authorization required.
Before proceeding, make sure 'http://127.0.0.1:5543/callback' is added as a Redirect URI
in your Spotify app settings at https://developer.spotify.com/dashboard
Open this URL in your browser to authorize the bot:
https://accounts.spotify.com/authorize?...
Waiting for authorization... (stop the bot with Ctrl+C to cancel)
```

Open the printed URL in a browser, log in with a Spotify account, and grant access. The bot exchanges the authorization code for a refresh token and stores it in the SQLite database. On all subsequent starts the stored token is loaded automatically — no browser interaction needed again.

> **Running on a remote server?** The callback listener binds to `127.0.0.1:5543` on the machine running the bot. Forward the port over SSH before opening the authorization URL in your local browser:
> ```
> ssh -N -L 5543:127.0.0.1:5543 user@your-server
> ```
> With the tunnel open, Spotify will redirect your browser to `http://127.0.0.1:5543/callback` and the bot will receive it through the tunnel. The bot also prints this command as a reminder in its log output during the authorization flow.

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
docker run -d --network host --env-file .env discord-music-bot
```

### Pull from GitHub Container Registry

```bash
docker pull ghcr.io/kiryl-kvit/discord-music-bot:latest
docker run -d --network host --env-file .env ghcr.io/kiryl-kvit/discord-music-bot:latest
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

> **Spotify first-run:** all Docker examples use `--network host` / `network_mode: host`, so the bot's callback listener on `127.0.0.1:5543` is directly on the host's loopback interface. Use the SSH tunnel approach from section 5.3 to reach it from your local browser. Run the container in the foreground the first time so the authorization URL is visible in the logs. Once the token is stored in the database the port is no longer used.

See the [environment variables reference](../README.md#environment-variables) for all `.env` options.
