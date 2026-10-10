# database/

MySQL schema for HistoryMap, kept as **versioned migration scripts** in `migrations/`.

- Files are numbered and run in order, never edited after they've been applied:
  `001_create_tables.sql`, `002_...sql`, and so on.
- `migrations/001_create_tables.sql` (places, summaries, photos) is coming next.
- To change the schema, add a new numbered migration instead of changing an old one.
