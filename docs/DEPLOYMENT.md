# Deployment Guide

## 1. Create a Discord Application

Follow the official Discord guide to create a new application and bot user:
[Setting up a bot application](https://discord.com/developers/docs/quick-start/getting-started#step-1-creating-an-app)

From the application page, copy the following values for your `.env` file:
- **Application ID** → `APP_ID`
- **Public Key** → `PUBLIC_KEY`

## 2. Get the Bot Token

Navigate to the **Bot** tab and copy (or reset) the token:
- **Token** → `BOT_TOKEN`

## 3. Configure Privileged Gateway Intents

No privileged intents are required. The bot only uses unprivileged intents (`Guilds`, `GuildVoiceStates`, etc.), so you can leave all privileged intent toggles **disabled** on the Bot tab.

## 4. Invite the Bot to Your Server

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

## 5. Configure Spotify (optional)

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

## 6. Configure Suno (optional)

To enable Suno support, add the following to your `.env` file:

```env
SUNO_ENABLED=true
```

No API credentials are required. The bot fetches song metadata directly from suno.com pages and streams audio from Suno's public CDN.

If this variable is not set or set to `false`, Suno links are treated as unsupported.

## Docker Compose

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
SUNO_ENABLED=true
```

Then run:

```bash
docker compose up -d
```
