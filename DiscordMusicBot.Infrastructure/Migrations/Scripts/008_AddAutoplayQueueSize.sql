ALTER TABLE guild_settings ADD COLUMN autoplay_queue_size INTEGER CHECK(autoplay_queue_size >= 1 AND autoplay_queue_size <= 50);
