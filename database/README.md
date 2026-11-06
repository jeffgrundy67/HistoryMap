# History Map — Database

Azure SQL schema and seed tooling for the History Map API.

## Migrations

| File | Purpose |
|------|---------|
| `migrations/001_create_tables.sql` | Creates `Places`, `Summaries`, `Photos`, `ViewCounts`, `SummaryQueue`, `SchemaMigrations` |

Apply against Azure SQL (SSMS, Azure Data Studio, or `sqlcmd`):

```bash
sqlcmd -S <server>.database.windows.net -d HistoryMap -U <user> -P '<password>' \
  -i migrations/001_create_tables.sql
```

The script is idempotent for this migration id via `SchemaMigrations`.

## Review notes (short)

- **Places.Ref** — `VARCHAR(12)` PK matches inventory / NHL-style refs; keep it stable as the join key everywhere.
- **Lat/Long** — `DECIMAL(9,6)` + composite index `(Latitude, Longitude)` for bbox pin queries; good enough for map zoom without geography type cost.
- **Summaries** — 1:1 with Places (`Ref` PK/FK). Text is `NVARCHAR(MAX)`; source metadata stays optional for review.
- **Photos** — store **BlobPath only** (relative to container). API builds public/CDN URLs from config; never store binary in SQL.
- **ViewCounts** — anonymous daily totals by `(Ref, ViewDay, AppName)`. No user ids, IPs, or device ids.
- **SummaryQueue** — one row per place for later on-demand summarization; status machine is constrained by check.
- **SchemaMigrations** — records applied scripts so deploys can skip already-run migrations.

## Seed / import (once)

After Azure SQL credentials exist:

1. Place inventory / `summaries.json` and photo inventory under `database/seed/` (gitignored samples only).
2. Run the API seed importer stub: `dotnet run --project api -- --seed` (see API README / `Tools/SeedImporter.cs`).
3. Upload photo binaries to the Blob container so paths match `Photos.BlobPath`.

Do not commit real connection strings or production seed dumps.
