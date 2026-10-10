package com.jeffgrundy.historicplaces

/**
 * Simple facade so [MainActivity] does not care *where* summaries live.
 *
 * Beginner tip: construct with [AssetSummarySource] for now.
 * To use your own server later, implement [SummarySource] as RemoteSummarySource
 * and pass that in instead — UI code stays the same.
 */
class SummaryRepository(
    private val source: SummarySource
) {
    fun ensureLoaded() {
        source.load()
    }

    fun summaryFor(refNum: String): PlaceSummary? = source.get(refNum)
}
