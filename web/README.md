# History Map — Web (placeholder)

Production web map remains at `jsgrundy.com/historymap/`.

This folder is reserved for the future monorepo copy of the web client. After the Azure API is live and seeded, the web app will switch pin/detail/photo calls to:

- `GET /api/v1/places?bbox=&zoom=`
- `GET /api/v1/places/{ref}`
- `GET /api/v1/places/with-photos`

No web client code in this scaffold — follow-on task after API verification.
