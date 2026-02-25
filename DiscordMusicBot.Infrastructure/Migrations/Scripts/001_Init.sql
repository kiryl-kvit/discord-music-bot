CREATE TABLE play_queue_items
(
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    guild_id    TEXT    NOT NULL,
    user_id     TEXT    NOT NULL,
    url         TEXT    NOT NULL,
    title       TEXT    NOT NULL,
    author      TEXT,
    duration_ms INTEGER,
    position    INTEGER NOT NULL,
    created_at  TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX ix_play_queue_items_guild_id ON play_queue_items (guild_id);
CREATE INDEX ix_play_queue_items_guild_position ON play_queue_items (guild_id, position);

CREATE TABLE guild_playback_state
(
    guild_id            TEXT    NOT NULL PRIMARY KEY,
    voice_channel_id    TEXT    NOT NULL,
    feedback_channel_id TEXT,
    resume_position_ms  INTEGER NOT NULL DEFAULT 0,
    resume_item_id      INTEGER,
    updated_at          TEXT    NOT NULL DEFAULT (datetime('now'))
);