# HistoryMap.Api

ASP.NET Core 10 API for History Map.

## Configuration

| Key | Description |
|-----|-------------|
| `ConnectionStrings:HistoryMap` | Azure SQL connection string |
| `BlobStorage:ContainerBaseUrl` | Blob/CDN container base URL (no trailing slash required) |
| `ApplicationInsights:ConnectionString` | Optional telemetry |

Environment variable form uses `__` (e.g. `BlobStorage__ContainerBaseUrl`).

## Endpoints

See root README. Swagger UI is enabled in Development.

## Seed

```bash
dotnet run --project . -- --seed --summaries ../database/seed/summaries.json --photos ../database/seed/photo-inventory.json
```
