ALTER TABLE play_queue_items ADD COLUMN played_at TEXT;

CREATE INDEX ix_play_queue_items_guild_played_at ON play_queue_items (guild_id, played_at);

CREATE TABLE guild_settings
(
    guild_id         TEXT    NOT NULL PRIMARY KEY,
    autoplay_enabled INTEGER NOT NULL DEFAULT 0
);
