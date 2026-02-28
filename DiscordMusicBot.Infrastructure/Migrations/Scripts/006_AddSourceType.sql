ALTER TABLE play_queue_items ADD COLUMN source_type TEXT;

UPDATE play_queue_items SET source_type = 'Suno'
WHERE url LIKE '%suno.com%';

UPDATE play_queue_items SET source_type = 'YouTube'
WHERE source_type IS NULL;
