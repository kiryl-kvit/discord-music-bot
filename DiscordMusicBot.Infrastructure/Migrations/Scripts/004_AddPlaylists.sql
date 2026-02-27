CREATE TABLE playlists
(
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id           TEXT    NOT NULL,
    name              TEXT    NOT NULL,
    track_count       INTEGER NOT NULL DEFAULT 0,
    total_duration_ms INTEGER,
    created_at        TEXT    NOT NULL DEFAULT (datetime('now')),
    updated_at        TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE UNIQUE INDEX ix_playlists_user_name ON playlists (user_id, name);
CREATE INDEX ix_playlists_user_id ON playlists (user_id);

CREATE TABLE playlist_items
(
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    playlist_id   INTEGER NOT NULL REFERENCES playlists (id) ON DELETE CASCADE,
    position      INTEGER NOT NULL,
    url           TEXT    NOT NULL,
    title         TEXT    NOT NULL,
    author        TEXT,
    duration_ms   INTEGER,
    thumbnail_url TEXT
);

CREATE INDEX ix_playlist_items_playlist_position ON playlist_items (playlist_id, position);
