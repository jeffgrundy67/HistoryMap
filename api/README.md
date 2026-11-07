# HistoryMap.Api

ASP.NET Core 10 API for History Map.

## Configuration

| Key | Description |
|-----|-------------|
| `ConnectionStrings:HistoryMap` | Azure SQL connection string |
| `BlobStorage:ContainerBaseUrl` | Blob/CDN container base URL (no trailing slash required) |
| `ApplicationInsights:ConnectionString` | Optional telemetry |
| `AzureAd:TenantId` / `ClientId` / `Audience` | Entra ID for ingest write APIs |

Environment variable form uses `__` (e.g. `BlobStorage__ContainerBaseUrl`, `AzureAd__ClientId`).

## Endpoints

See root README and `docs/azure-ad-ingest.md`. Swagger UI (with Bearer scheme) is enabled in all environments.

## Seed

```bash
dotnet run --project . -- --seed --summaries ../database/seed/summaries.json --photos ../database/seed/photo-inventory.json
```
