# History Map

Monorepo for the History Map shared backend (Azure) and future web/Android clients.

**Repo:** https://github.com/jeffgrundy67/HistoryMap

## Layout

| Path | Purpose |
|------|---------|
| `api/` | ASP.NET Core 10 JSON API (`/api/v1/...`) |
| `database/` | Azure SQL T-SQL migrations + seed stubs |
| `web/` | Placeholder — production web stays on jsgrundy.com/historymap for now |
| `android/` | Placeholder — Android client later |
| `tests/` | xUnit tests for API helpers/services |
| `.github/workflows/` | Build, test, deploy to App Service on `main` |

## API (first cut)

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| GET | `/api/v1/places?bbox=minLng,minLat,maxLng,maxLat&zoom=` | Anonymous | Map pins |
| GET | `/api/v1/places/{ref}` | Anonymous | Place + summary + photo **URLs** |
| GET | `/api/v1/places/with-photos` | Anonymous | Refs with ≥1 photo |
| GET | `/health` or `/api/v1/health` | Anonymous | Liveness + DB probe |
| PUT/POST | `/api/v1/ingest/places/{ref}` | Entra Bearer | Upsert place + summary |
| PUT/POST | `/api/v1/ingest/photos` | Entra Bearer | Upsert photo metadata |
| POST | `/api/v1/ingest/batch` | Entra Bearer | Batch upsert |

Photos: SQL stores relative `BlobPath` only. Responses use `BlobStorage:ContainerBaseUrl` + path. Blob **binary** upload is separate (see `docs/azure-ad-ingest.md`).

## Local development

```bash
# .NET 10 SDK required (current LTS)
dotnet restore HistoryMap.sln
dotnet build HistoryMap.sln
dotnet test HistoryMap.sln

# Run API (set connection string / blob base URL via user-secrets or env)
dotnet user-secrets --project api init
dotnet user-secrets --project api set "ConnectionStrings:HistoryMap" "<azure-sql>"
dotnet user-secrets --project api set "BlobStorage:ContainerBaseUrl" "https://<acct>.blob.core.windows.net/photos"
dotnet run --project api
```

Apply schema: see `database/README.md`. Seed stub:

```bash
dotnet run --project api -- --seed \
  --summaries database/seed/summaries.json \
  --photos database/seed/photo-inventory.json
```

## Azure

- Infra runbook: **[docs/azure-setup.md](docs/azure-setup.md)** (Project store copy may also exist)
- **Entra ingest (Grok Bot):** **[docs/azure-ad-ingest.md](docs/azure-ad-ingest.md)** — portal checklist, App Service keys, client_credentials example

Required App Service / GitHub secrets:

- `ConnectionStrings__HistoryMap` — Azure SQL
- `BlobStorage__ContainerBaseUrl` — public container (or CDN) base URL
- `APPLICATIONINSIGHTS_CONNECTION_STRING` — optional but wired
- `AZURE_WEBAPP_PUBLISH_PROFILE` — GitHub Actions deploy secret
- `AzureAd__TenantId` / `AzureAd__ClientId` / `AzureAd__Audience` — Entra for write APIs (see ingest doc)

App Service runtime: **.NET 10**.

## Out of scope (this scaffold)

Play Store packaging, full photo binary upload automation, on-demand PDF summarization, production web/Android cutover until the API is verified.
