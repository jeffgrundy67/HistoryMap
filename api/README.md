# api/ (planned)

This folder will hold a small PHP API that both the web app and the Android app read from, replacing the static `summaries.json` / `photos.json` files.

## Plan

- **Versioned routes** under `/api/v1/` (for example `/api/v1/places/{ref}`, `/api/v1/places?bbox=...`, `/api/v1/photos`), so future changes can ship as `/api/v2/` without breaking older app builds.
- **JSON responses** with one shape for both clients (web and Android).
- **MySQL** via PDO with **prepared statements only**: no string-built SQL.
- **Configuration outside the repo**: copy `config.example.php` to `config.php` (git-ignored) or to a folder above `public_html`, and fill in the real database settings there. No credentials are ever committed.
- Read-only public endpoints first; anything that writes (for example a view counter) gets input validation and rate limiting.

Nothing here is live yet.
