CREATE TABLE favorite_items
(
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id     TEXT    NOT NULL,
    url         TEXT    NOT NULL,
    title       TEXT    NOT NULL,
    alias       TEXT,
    author      TEXT,
    duration_ms INTEGER,
    is_playlist INTEGER NOT NULL DEFAULT 0,
    created_at  TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE UNIQUE INDEX ix_favorite_items_user_url ON favorite_items (user_id, url);
CREATE INDEX ix_favorite_items_user_id ON favorite_items (user_id);
