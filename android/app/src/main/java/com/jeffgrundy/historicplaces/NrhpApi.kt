package com.jeffgrundy.historicplaces

import android.net.Uri
import android.util.Log
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL

/**
 * Talks to the National Park Service ArcGIS MapServer for NRHP point locations.
 *
 * Docs / service:
 * https://mapservices.nps.gov/arcgis/rest/services/cultural_resources/nrhp_locations/MapServer/0
 *
 * We query by a geographic bounding box (the visible map area).
 */
object NrhpApi {

    private const val TAG = "NrhpApi"
    private const val ENDPOINT =
        "https://mapservices.nps.gov/arcgis/rest/services/cultural_resources/nrhp_locations/MapServer/0/query"

    // Keep responses manageable for a phone map.
    private const val MAX_RECORDS = 500

    private const val OUT_FIELDS =
        "NRIS_Refnum,RESNAME,ResType,Address,City,County,State,STATUS,Is_NHL,CertDate,NARA_URL"

    /**
     * Fetch NRHP points that intersect [west],[south],[east],[north] (WGS84 degrees).
     * Runs on whatever thread you call it from — use a background dispatcher from the Activity.
     */
    fun fetchInBounds(west: Double, south: Double, east: Double, north: Double): List<NrhpPlace> {
        // ArcGIS envelope geometry string: xmin,ymin,xmax,ymax
        val geometry = "$west,$south,$east,$north"

        val uri = Uri.parse(ENDPOINT).buildUpon()
            .appendQueryParameter("geometry", geometry)
            .appendQueryParameter("geometryType", "esriGeometryEnvelope")
            .appendQueryParameter("inSR", "4326")
            .appendQueryParameter("spatialRel", "esriSpatialRelIntersects")
            .appendQueryParameter("outFields", OUT_FIELDS)
            .appendQueryParameter("returnGeometry", "true")
            .appendQueryParameter("outSR", "4326")
            .appendQueryParameter("resultRecordCount", MAX_RECORDS.toString())
            .appendQueryParameter("f", "json")
            .build()

        Log.d(TAG, "GET $uri")

        val conn = (URL(uri.toString()).openConnection() as HttpURLConnection).apply {
            connectTimeout = 15000
            readTimeout = 20000
            requestMethod = "GET"
            // Be a good citizen: identify the app.
            setRequestProperty("User-Agent", "HistoricPlaces/1.0 (com.jeffgrundy.historicplaces)")
        }

        try {
            val code = conn.responseCode
            val body = (if (code in 200..299) conn.inputStream else conn.errorStream)
                .bufferedReader()
                .use { it.readText() }

            if (code !in 200..299) {
                throw IllegalStateException("NRHP HTTP $code: ${body.take(200)}")
            }

            return parseFeatures(body)
        } finally {
            conn.disconnect()
        }
    }

    private fun parseFeatures(jsonText: String): List<NrhpPlace> {
        val root = JSONObject(jsonText)
        if (root.has("error")) {
            throw IllegalStateException("NRHP error: ${root.getJSONObject("error")}")
        }

        val features = root.optJSONArray("features") ?: return emptyList()
        val out = ArrayList<NrhpPlace>(features.length())

        for (i in 0 until features.length()) {
            val feat = features.getJSONObject(i)
            val attrs = feat.getJSONObject("attributes")
            val geom = feat.optJSONObject("geometry") ?: continue

            val lat = geom.optDouble("y", Double.NaN)
            val lon = geom.optDouble("x", Double.NaN)
            if (lat.isNaN() || lon.isNaN()) continue

            val nhlRaw = attrs.optString("Is_NHL", "")
            val isNhl = nhlRaw.equals("X", ignoreCase = true) ||
                nhlRaw.equals("Y", ignoreCase = true) ||
                nhlRaw.equals("true", ignoreCase = true)

            out.add(
                NrhpPlace(
                    refNum = attrs.optString("NRIS_Refnum", ""),
                    name = attrs.optString("RESNAME", "(unnamed)"),
                    type = attrs.optString("ResType", ""),
                    address = attrs.optString("Address", ""),
                    city = attrs.optString("City", ""),
                    county = attrs.optString("County", ""),
                    status = attrs.optString("STATUS", ""),
                    isNhl = isNhl,
                    // CertDate is a string from the service (often "YYYY-MM-"); show as-is.
                    certDate = attrs.optString("CertDate", ""),
                    naraUrl = attrs.optString("NARA_URL", ""),
                    lat = lat,
                    lon = lon
                )
            )
        }

        // Dedupe by ref number (linear resources can appear as multiple points).
        return out.distinctBy { it.refNum.ifBlank { "${it.lat},${it.lon},${it.name}" } }
    }
}
