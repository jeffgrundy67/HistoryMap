# Azure AD (Entra ID) ingest for HistoryMap

Protect write/ingest APIs so **Grok Bot** (or any daemon) can upsert NRHP places, summaries, and photo metadata with **client credentials**. Public map GETs stay anonymous.

Live API base (default hostname):

`https://historymap-api-ded3c7g5hhf8avcn.westcentralus-01.azurewebsites.net`

Swagger (Bearer scheme registered): `/swagger`

---

## Endpoints (all require `Authorization: Bearer`)

| Method | Path | Body |
|--------|------|------|
| `PUT` / `POST` | `/api/v1/ingest/places/{ref}` | Place + optional summary fields |
| `PUT` / `POST` | `/api/v1/ingest/places` | Same; `ref` in JSON body |
| `PUT` / `POST` | `/api/v1/ingest/photos` | Photo metadata (`ref` + `blobPath`) |
| `POST` | `/api/v1/ingest/batch` | `{ "places": [...], "photos": [...] }` |

**Still anonymous:** `GET /api/v1/places*`, `GET /health`, `GET /api/v1/health`, Swagger.

### Place / summary JSON (matches seed importer)

```json
{
  "ref": "66000045",
  "name": "Example Historic Place",
  "latitude": 38.9072,
  "longitude": -77.0369,
  "city": "Washington",
  "state": "DC",
  "placeType": "building",
  "isNhl": false,
  "listedDate": "1970-01-15",
  "summaryText": "…",
  "sourceHeading": "NRHP",
  "confidence": 0.85,
  "sourceUrl": "https://…",
  "reviewNote": null
}
```

Idempotent by **Place Ref**. Re-PUT updates fields that are present.

### Photo metadata JSON

```json
{
  "ref": "66000045",
  "blobPath": "places/66000045/main.jpg",
  "caption": "Façade",
  "source": "NPS",
  "widthPx": 1600,
  "heightPx": 1200,
  "sortOrder": 0
}
```

Idempotent by **Ref + BlobPath**. Place must already exist (upsert place first).

### Blob binary upload (Phase 2)

This API stores **metadata only**. Upload the image bytes to Azure Blob container `photos` with the same path as `blobPath` (portal, AzCopy, or SDK). A future authenticated upload endpoint is optional; not required for Grok Bot if the bot already has a storage SAS or managed identity.

---

## App Service configuration keys

Set on App Service → **Environment variables** / Configuration (never commit secrets):

| App setting | Example / notes |
|-------------|-----------------|
| `AzureAd__TenantId` | Directory (tenant) ID |
| `AzureAd__ClientId` | **API** app registration Application (client) ID |
| `AzureAd__Audience` | `api://<api-client-id>` **or** the API client ID (must match token `aud`) |
| `AzureAd__Instance` | `https://login.microsoftonline.com/` (optional; this is the default) |

Existing keys remain: `ConnectionStrings__HistoryMap`, `BlobStorage__ContainerBaseUrl`, etc.

Restart the web app after saving.

---

## Entra portal checklist (Jeff)

### A. API app registration (HistoryMap API)

1. Entra ID → **App registrations** → **New registration**.
2. Name: `HistoryMap-API` (or similar). Accounts in this organizational directory only.
3. Note **Application (client) ID** → becomes `AzureAd__ClientId`.
4. Note **Directory (tenant) ID** → becomes `AzureAd__TenantId`.
5. **Expose an API**:
   - Set Application ID URI: `api://<api-client-id>` (recommended).
   - Add a scope (optional, for delegated clients): `access_as_application` — Admin consent, Admins only is fine.
6. **App roles** → Create:
   - Display name: `HistoryMap Ingest Write`
   - Value: `HistoryMap.Ingest.Write`
   - Allowed member types: **Applications**
   - Description: Upsert places, summaries, photo metadata
   - Enable → Apply
7. **Token configuration**: optional; roles appear in access tokens for app-only callers automatically when assigned.

### B. Client app registration (Grok Bot)

1. **New registration**: `HistoryMap-GrokBot` (or similar).
2. **Certificates & secrets** → New client secret → store in Bitwarden / bot secret store (**not** git).
3. **API permissions** → Add a permission → **My APIs** → `HistoryMap-API` → **Application permissions** → `HistoryMap.Ingest.Write` → Add.
4. Click **Grant admin consent** for the tenant.
5. (Alternative / also OK) Entra **Enterprise applications** → `HistoryMap-API` → **Users and groups** / **App role assignments** → assign the Grok Bot service principal the `HistoryMap.Ingest.Write` role.

### C. App Service

1. Set `AzureAd__TenantId`, `AzureAd__ClientId`, `AzureAd__Audience` as above.
2. Prefer `Audience` = Application ID URI (`api://…`) if that is what appears in token `aud`; if tokens show the raw client GUID as `aud`, set Audience to that GUID.
3. Restart → open `/swagger` → Authorize with a Bearer token to try ingest.

---

## Grok Bot calling example (client credentials)

```bash
# 1) Token
TENANT="<tenant-id>"
API_APP_ID="<api-client-id>"          # HistoryMap-API
CLIENT_ID="<grok-bot-client-id>"
CLIENT_SECRET="<grok-bot-secret>"

TOKEN=$(curl -s -X POST "https://login.microsoftonline.com/${TENANT}/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=${CLIENT_ID}" \
  -d "client_secret=${CLIENT_SECRET}" \
  -d "scope=api://${API_APP_ID}/.default" \
  -d "grant_type=client_credentials" | jq -r .access_token)

# 2) Upsert place + summary
curl -s -X PUT "https://historymap-api-ded3c7g5hhf8avcn.westcentralus-01.azurewebsites.net/api/v1/ingest/places/66000045" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name":"Example","latitude":38.9,"longitude":-77.0,"city":"Washington","state":"DC","summaryText":"…","confidence":0.9}'

# 3) Upsert photo metadata (after uploading blob to places/66000045/main.jpg)
curl -s -X PUT "https://historymap-api-ded3c7g5hhf8avcn.westcentralus-01.azurewebsites.net/api/v1/ingest/photos" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"ref":"66000045","blobPath":"places/66000045/main.jpg","caption":"Main","sortOrder":0}'
```

C# sketch (MSAL):

```csharp
var app = ConfidentialClientApplicationBuilder
    .Create(clientId)
    .WithClientSecret(clientSecret)
    .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
    .Build();

var result = await app.AcquireTokenForClient(new[] { $"api://{apiClientId}/.default" }).ExecuteAsync();
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
await http.PutAsJsonAsync($"{baseUrl}/api/v1/ingest/places/{nrhpRef}", placeBody);
```

Authorization policy accepts either:

- App role claim `HistoryMap.Ingest.Write`, or
- Scope `access_as_application`

---

## Local development

- Leave `AzureAd:TenantId` / `ClientId` empty → JWT scheme still registered; ingest calls without a real token get **401**.
- Seed CLI (`dotnet run --project api -- --seed …`) does not need Entra; it uses `IIngestService` in-process.
- Integration tests use a `Test` auth scheme via `WebApplicationFactory`.

---

## Security notes

- Do **not** commit client secrets, publish profiles, or SQL passwords.
- Prefer app roles (application permissions) for daemon bots over user-delegated scopes.
- Rotate the Grok Bot client secret periodically; store only in the bot’s secret store / App Service if you proxy.
