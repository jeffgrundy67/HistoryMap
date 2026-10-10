package com.jeffgrundy.historicplaces

import android.content.Context
import android.util.Log
import org.json.JSONObject

/**
 * Reads summaries from app/src/main/assets/summaries.json.
 *
 * JSON shape (object keyed by ref_num):
 * {
 *   "66000152": { "summary": "...", "heading": "HISTORY" },
 *   ...
 * }
 */
class AssetSummarySource(
    private val context: Context,
    private val assetFileName: String = "summaries.json"
) : SummarySource {

    private var cache: Map<String, PlaceSummary>? = null

    override fun load(): Map<String, PlaceSummary> {
        cache?.let { return it }

        return try {
            val text = context.assets.open(assetFileName).bufferedReader().use { it.readText() }
            val root = JSONObject(text)
            val map = HashMap<String, PlaceSummary>()
            val keys = root.keys()
            while (keys.hasNext()) {
                val ref = keys.next()
                val obj = root.getJSONObject(ref)
                map[ref] = PlaceSummary(
                    summary = obj.optString("summary", ""),
                    heading = obj.optString("heading", "")
                )
            }
            cache = map
            Log.d(TAG, "Loaded ${map.size} summaries from assets/$assetFileName")
            map
        } catch (e: Exception) {
            Log.e(TAG, "Could not load $assetFileName", e)
            emptyMap()
        }
    }

    override fun get(refNum: String): PlaceSummary? {
        val all = load()
        return all[refNum] ?: all[refNum.trimStart('0').ifEmpty { "0" }]
    }

    companion object {
        private const val TAG = "AssetSummarySource"
    }
}
