package com.jeffgrundy.historicplaces

/**
 * Short history blurb for one NRHP place, keyed by National Register reference number.
 *
 * [heading] is the nomination section we summarized from (e.g. "STATEMENT OF SIGNIFICANCE").
 */
data class PlaceSummary(
    val summary: String,
    val heading: String = ""
)
