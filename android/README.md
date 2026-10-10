# Historic Places

A beginner-friendly Android app that shows nearby **National Register of Historic Places (NRHP)** listings on a map.

- **Map:** [osmdroid](https://github.com/osmdroid/osmdroid) with **Esri World Street Map** tiles (no Google Maps API key, no billing).
- **Data:** Live queries to the National Park Service ArcGIS MapServer for unrestricted NRHP points.
- **Location:** Centers on you when allowed; otherwise downtown Montgomery, Alabama.

Package: `com.jeffgrundy.historicplaces`

## What you need

1. **Android Studio** (latest stable, Standard install is fine).
2. A physical Android phone (or emulator) with USB debugging / wireless debugging enabled.
3. Internet on the phone (tiles + NRHP data are downloaded live).

## Open the project

1. Clone the HistoryMap repo.
2. In Android Studio: **File → Open…** and select the `android` folder (the one that contains `settings.gradle.kts`).
3. Wait for Gradle Sync to finish (first time downloads libraries).
4. Plug in your phone, accept the debugging prompt, then click the green **Run** button.

If Android Studio asks to install an SDK platform or accept licenses, say yes.

## Build from the command line (optional)

```bash
cd android
./gradlew assembleDebug        # Windows: gradlew.bat assembleDebug
```

The debug APK lands at `app/build/outputs/apk/debug/app-debug.apk`.

## How the app works (short tour)

| File | Role |
|---|---|
| `MainActivity.kt` | Map UI, location permission, debounced NRHP fetch, markers, bottom sheet |
| `NrhpApi.kt` | Builds the MapServer query URL and parses JSON into `NrhpPlace` |
| `NrhpPlace.kt` | Simple data class for one listing |

When the map stops moving, the app reads the visible bounding box and calls:

`…/nrhp_locations/MapServer/0/query` with `geometryType=esriGeometryEnvelope`, `inSR=4326`, `outSR=4326`.

National Historic Landmarks (`Is_NHL == "X"`) use a **gold** pin; other listings use **blue**.

## Important notes

- Many NRHP sites are **private property** and are not open to visitors. Please be respectful.
- Sensitive / address-restricted sites are **not** included in the public MapServer layer.
- Esri tile attribution is required (shown in the footer).
- OSM’s volunteer tile servers are **not** used here (they block many apps). Google Maps is avoided to skip API keys.

## Sharing on GitHub

This repo already includes a `.gitignore` for Android. Do **not** commit `local.properties`, `build/`, or `.gradle/`.


## History summaries

Selected places show a **History** blurb in the details sheet (from National Register nomination PDFs via NARA).

- Bundled file: `app/src/main/assets/summaries.json` (keyed by National Register `ref_num`).
- Currently includes **100** offline summaries: 25 near downtown Montgomery, AL; 25 near downtown Portland, OR; 25 near downtown Salem, OR; 25 near Catonsville, MD (300 Marianne Drive / Marianne Lane area).
- Loaded through `SummaryRepository` + `AssetSummarySource`.
- Later you can host the same JSON on your own server and add a `RemoteSummarySource` without rewriting the UI.
