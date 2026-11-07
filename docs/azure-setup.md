# History Map — Azure setup runbook

Step-by-step for Jeff: free-tier App Service + Azure SQL + Blob Storage, secrets, a **$5** budget alert, and optional `api.jsgrundy.com` CNAME. Create resources in the Azure portal (or CLI after login). Do this before expecting the GitHub Actions deploy or seed importer to succeed.

Target repo: [jeffgrundy67/HistoryMap](https://github.com/jeffgrundy67/HistoryMap)

---

## 1. Prerequisites

- Azure subscription you own (free / student / pay-as-you-go is fine).
- GitHub repo access to push the monorepo and add Actions secrets.
- Optional: custom domain DNS for `jsgrundy.com` (cPanel already confirmed for CNAME).

Suggested names (change if taken):

| Resource | Suggested name |
|----------|----------------|
| Resource group | `rg-historymap` |
| Region | East US (or closest cheap region) |
| App Service plan | `asp-historymap-free` |
| Web App | `historymap-api` |
| SQL server | `sql-historymap-<unique>` |
| SQL database | `HistoryMap` |
| Storage account | `sthistorymap<unique>` |
| Blob container | `photos` |
| Application Insights | `appi-historymap` |

---

## 2. Budget alert ($5) — do this first

1. Portal → **Cost Management + Billing** → **Budgets** → **Add**.
2. Scope: your subscription (or the resource group once created).
3. Budget amount: **5** USD, reset period Monthly.
4. Alert condition: Actual ≥ **100%** of budget (and optionally Forecasted ≥ 100%).
5. Add your email as the alert recipient → Create.

This does not hard-cap spend by itself; it warns you. Stay on free SKUs below to keep cost near zero.

---

## 3. Resource group

1. **Resource groups** → Create → `rg-historymap` → same region as above → Review + create.

---

## 4. Azure SQL (free offer)

1. **SQL databases** → Create.
2. Subscription + resource group `rg-historymap`.
3. Database name: `HistoryMap`.
4. Server → Create new:
   - Server name: globally unique (`sql-historymap-…`).
   - Authentication: SQL authentication — save admin login/password in a password manager.
   - Allow Azure services to access the server: **Yes**.
5. Compute + storage:
   - Prefer the **Free** offer / serverless / Basic as available in your subscription (Azure SQL free DB offer when eligible).
   - If free is unavailable, use the cheapest **Basic** or serverless with auto-pause.
6. Networking: add your client IP for SSMS / `sqlcmd` during setup; keep “Allow Azure services” on for App Service.
7. Create → wait until Running.

**Connection string** (ADO.NET): Portal → SQL database → Connection strings → copy ADO.NET, insert password:

```text
Server=tcp:sql-historymap-<unique>.database.windows.net,1433;Initial Catalog=HistoryMap;Persist Security Info=False;User ID=<admin>;Password=<password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Apply schema from the monorepo:

```bash
sqlcmd -S sql-historymap-<unique>.database.windows.net -d HistoryMap -U <admin> -P '<password>' \
  -i database/migrations/001_create_tables.sql
```

(Or paste/run the script in Azure Data Studio / SSMS.)

---

## 5. Blob Storage (photos)

1. **Storage accounts** → Create → `rg-historymap` → name `sthistorymap…` → Standard / LRS is enough.
2. Create → **Containers** → `+ Container` → name `photos` → Public access level:
   - **Blob** (anonymous read for blobs only) if the map should load images without SAS, **or**
   - Private + CDN/SAS later (then adjust API URL strategy).
3. Note the container base URL:

```text
https://sthistorymap<unique>.blob.core.windows.net/photos
```

Upload objects so paths match `Photos.BlobPath` (e.g. `places/REF/main.jpg`). SQL stores paths only; the API returns `ContainerBaseUrl + "/" + BlobPath`.

---

## 6. Application Insights (optional, AZ-900 practice)

1. Create **Application Insights** workspace-based → `appi-historymap` in `rg-historymap`.
2. Copy **Connection string**.
3. The API already calls `AddApplicationInsightsTelemetry()`; set the connection string as an App Setting (below).

---

## 7. App Service (free tier)

1. **App Services** → Create.
2. Name: `historymap-api` (URL becomes `https://historymap-api.azurewebsites.net`).
3. Publish: **Code** · Runtime: **.NET 10 (LTS)** · OS: **Linux** (or Windows if you prefer).
4. App Service plan → Create new → **Free F1** (if F1 unavailable in region, pick another region or the cheapest shared tier).
5. Review + create.

### App settings / connection strings

App Service → **Configuration** (or Environment variables):

| Name | Value |
|------|--------|
| `ConnectionStrings__HistoryMap` | Azure SQL ADO.NET string |
| `BlobStorage__ContainerBaseUrl` | `https://st….blob.core.windows.net/photos` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | App Insights connection string |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

Save → Restart the app after first deploy.

### Publish profile (for GitHub Actions)

App Service → **Download publish profile** → contents become the GitHub secret `AZURE_WEBAPP_PUBLISH_PROFILE`.

If you rename the web app, update `AZURE_WEBAPP_NAME` in `.github/workflows/api-ci-cd.yml`.

---

## 8. GitHub secrets

Repo → **Settings** → **Secrets and variables** → **Actions** → New repository secret:

| Secret | Purpose |
|--------|---------|
| `AZURE_WEBAPP_PUBLISH_PROFILE` | Full publish profile XML from App Service |

Connection strings can live only in App Service settings (preferred) so they are not required in GitHub. If you also use them in workflows later, add matching secrets — never commit them.

Push to `main` (or run the workflow manually) to build, test, and deploy.

Smoke checks after deploy:

```text
GET https://historymap-api.azurewebsites.net/health
GET https://historymap-api.azurewebsites.net/api/v1/places/with-photos
```

---

## 9. Seed data (once SQL + Blob are ready)

1. Put real `summaries.json` and `photo-inventory.json` under `database/seed/` (gitignored).
2. Upload photo binaries to the `photos` container with matching paths.
3. From a machine that can reach Azure SQL:

```bash
dotnet run --project api -- --seed \
  --summaries database/seed/summaries.json \
  --photos database/seed/photo-inventory.json
```

---

## 10. Optional: `api.jsgrundy.com` CNAME

DNS (cPanel) already supports CNAME — when ready:

1. App Service → **Custom domains** → Add → `api.jsgrundy.com`.
2. Create CNAME: `api` → `historymap-api.azurewebsites.net` (or the hostname Azure shows).
3. Validate domain → Add managed certificate (free) for HTTPS.
4. Point web/Android API base URL to `https://api.jsgrundy.com`.

Leave this until the default `*.azurewebsites.net` endpoint is healthy.

---

## Checklist

- [ ] $5 monthly budget alert with email
- [ ] Resource group `rg-historymap`
- [ ] Azure SQL `HistoryMap` + firewall for your IP + Azure services
- [ ] Ran `001_create_tables.sql`
- [ ] Storage account + `photos` container + base URL noted
- [ ] App Service F1 `.NET 10` + app settings
- [ ] Publish profile → GitHub `AZURE_WEBAPP_PUBLISH_PROFILE`
- [ ] Deploy from `main` succeeds; `/health` returns healthy
- [ ] Seed importer run; sample place detail shows Blob URLs
- [ ] Entra ingest for Grok Bot — see [azure-ad-ingest.md](azure-ad-ingest.md)
- [ ] (Later) CNAME `api.jsgrundy.com` + TLS

## What stays out of this pass

Play Store packaging, bulk photo backfill automation, on-demand summarization workers, and switching production web/Android until the API is verified. Blob binary upload via authenticated API is Phase 2 (metadata ingest is documented in azure-ad-ingest.md).
