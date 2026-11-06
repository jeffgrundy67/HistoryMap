# Seed data

Drop production `summaries.json` and `photo-inventory.json` here (gitignored). Use the `*.sample.json` files as shape references.

Import via the API stub after Azure SQL is reachable:

```bash
dotnet run --project api -- --seed \
  --summaries database/seed/summaries.json \
  --photos database/seed/photo-inventory.json
```

Photo rows store relative `blobPath` only; upload matching blobs to the container configured in `BlobStorage:ContainerBaseUrl`.
