# HistoryMap

HistoryMap is a map of places listed on the U.S. **National Register of Historic Places (NRHP)**, with a short history summary for each place and, for some places, a photo from its National Register nomination.

- **Web app (live):** https://jsgrundy.com/historymap/
- **Android app:** `android/` (HistoricPlaces, Kotlin)

I started it to make the history behind nearby landmarks easy to read on a phone. Coverage of summaries and photos so far focuses on Alabama (especially Montgomery) and the Catonsville / Ellicott City area of Maryland.

## Features

- Map of NRHP listings loaded live from the National Park Service as you pan and zoom, with marker clustering. Pins are blue for listed places and gold for National Historic Landmarks.
- A detail panel (bottom sheet on phones, side panel on desktop) with the nomination photo, a plain-language history summary, address, county, type, listing date, a link to the nomination papers and directions.
- About 1,100 history summaries (`web/summaries.json`) and 125 nomination photos (`web/photos.json`, `web/photos/`).
- An "Only places with photos" filter that shows every place with a photo at any zoom level (`?photos=1`).
- "Show my location", plus deep links to a single place: `?ref=<NRIS reference number>`.
- The Android app has the same map and summaries (bundled in its assets); photos are web-only for now.

## Tech stack

- **Web:** a single static `index.html` with vanilla JavaScript, [Leaflet](https://leafletjs.com/) 1.9 + Leaflet.markercluster, and Esri World Street Map tiles. Data comes from static JSON files. Hosted on shared cPanel hosting, with an `.htaccess` that makes the HTML and JSON no-cache.
- **Android:** Kotlin, osmdroid (Esri tiles, no Google Maps key), coroutines; minSdk 24, targetSdk 35.
- **Data tools:** Python 3 and Bash, using poppler-utils (`pdftotext`, `pdftoppm`), Tesseract OCR and Pillow.
- **Planned:** MySQL with a PHP API (see the roadmap).

## Data sources

- **NPS NRHP locations:** the National Park Service ArcGIS MapServer (`cultural_resources/nrhp_locations`) provides the points, names, addresses and listing details, queried live.
- **Nomination documents:** National Archives (NARA) catalog PDFs, NPS NPGallery, and state historic preservation offices (Alabama Historical Commission, Maryland Historical Trust).
- **Summaries are AI-written** from the significance sections of the nomination documents and spot-checked. They can contain mistakes; the nomination papers are the authoritative source.
- **Photos** are cropped from the photographs in the nomination files. Captions (view, date) come from the nomination where available.

## Repository layout

```
historymap/
├── web/          Live web app: index.html, summaries.json, photos.json, photos/, .htaccess
├── android/      HistoricPlaces Android project (open this folder in Android Studio)
├── api/          Planned PHP API (/api/v1/) - README and config.example.php
├── database/     MySQL migrations (migrations/001_create_tables.sql coming next)
├── tests/        Planned PHPUnit tests + GitHub Actions
└── tools/        Data pipeline: photo extraction, summary merge, deploy helpers
```

## Roadmap

1. **MySQL database** for places, summaries and photos, built from versioned migrations in `database/`.
2. **PHP API** (`/api/v1/`, JSON, prepared statements, config kept outside the repo).
3. **Shared API for web and Android**, so both apps read the same data instead of bundled JSON files.
4. **Unit tests** with PHPUnit, run by GitHub Actions against a test database.
5. **View counter** per place.
6. **On-demand summaries** for places that don't have one yet.

## Credits and licensing

- **Code:** MIT License (see the `LICENSE` file at the repo root).
- **NRHP data and nomination photos:** public records of the National Park Service and the National Archives. Some nomination material comes from state historic preservation offices (the Alabama Historical Commission and the Maryland Historical Trust), credited in each photo's `source` URL.
- **Map tiles:** © Esri and its data providers. **Libraries:** Leaflet, Leaflet.markercluster, osmdroid, under their own licenses.
- Many listed places are private property; please view them from public areas.
