package com.jeffgrundy.historicplaces

/**
 * One National Register listing, parsed from the NPS MapServer JSON.
 *
 * Field names match what we ask for in [NrhpApi] (NRIS_Refnum, RESNAME, …).
 */
data class NrhpPlace(
    val refNum: String,
    val name: String,
    val type: String,
    val address: String,
    val city: String,
    val county: String,
    val status: String,
    val isNhl: Boolean,
    val certDate: String,
    val naraUrl: String,
    val lat: Double,
    val lon: Double
)
