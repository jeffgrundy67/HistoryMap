package com.jeffgrundy.historicplaces

/**
 * Where history summaries come from.
 *
 * Today we ship a small JSON file inside the APK ([AssetSummarySource]).
 * Later you can add something like:
 *
 *   class RemoteSummarySource(private val baseUrl: String) : SummarySource { ... }
 *
 * that GETs https://your-server.example/summaries.json (or /summaries/{refNum}.json)
 * without changing the rest of the UI — just pass that source into [SummaryRepository].
 */
interface SummarySource {
    /** Load (or refresh) all summaries. Safe to call more than once. */
    fun load(): Map<String, PlaceSummary>

    /** Look up one place by NRHP ref number (e.g. "66000152"). */
    fun get(refNum: String): PlaceSummary?
}
