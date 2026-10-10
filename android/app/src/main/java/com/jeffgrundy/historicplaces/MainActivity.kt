package com.jeffgrundy.historicplaces

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.Path
import android.graphics.drawable.BitmapDrawable
import android.graphics.Bitmap
import android.location.Location
import android.location.LocationListener
import android.location.LocationManager
import android.net.Uri
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.text.method.ScrollingMovementMethod
import android.view.View
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import com.google.android.material.bottomsheet.BottomSheetDialog
import com.jeffgrundy.historicplaces.databinding.ActivityMainBinding
import com.jeffgrundy.historicplaces.databinding.BottomSheetPlaceBinding
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.osmdroid.config.Configuration
import org.osmdroid.events.MapListener
import org.osmdroid.events.ScrollEvent
import org.osmdroid.events.ZoomEvent
import org.osmdroid.tileprovider.tilesource.OnlineTileSourceBase
import org.osmdroid.util.GeoPoint
import org.osmdroid.util.MapTileIndex
import org.osmdroid.views.CustomZoomButtonsController
import org.osmdroid.views.overlay.Marker
import org.osmdroid.bonuspack.clustering.RadiusMarkerClusterer

/**
 * Single-screen app:
 *  1) Show a street map (Esri tiles via osmdroid — no Google/API key).
 *  2) Center on the user (or Montgomery if location is unavailable).
 *  3) When the map stops moving, ask the NPS MapServer for NRHP points in view.
 *  4) Tap a marker to see details + Nomination / Directions buttons.
 */
class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding

    // History blurbs keyed by NRHP ref number (bundled JSON for now).
    private lateinit var summaryRepo: SummaryRepository

    // Downtown Montgomery fallback (verified lat/lon from earlier research).
    private val montgomery = GeoPoint(32.3777, -86.3006)

    private val mainHandler = Handler(Looper.getMainLooper())
    private var fetchRunnable: Runnable? = null
    private var fetchJob: Job? = null

    private var clusterOverlay: RadiusMarkerClusterer? = null
    private var blueIcon: BitmapDrawable? = null
    private var goldIcon: BitmapDrawable? = null

    private val scope = CoroutineScope(Dispatchers.Main + Job())

    // Debounce: wait this long after the user stops panning/zooming before querying.
    private val debounceMs = 650L

    private val permissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { grants ->
        val granted = grants[Manifest.permission.ACCESS_FINE_LOCATION] == true ||
            grants[Manifest.permission.ACCESS_COARSE_LOCATION] == true
        if (granted) {
            centerOnUser()
        } else {
            Toast.makeText(
                this,
                "Location denied — showing Montgomery, AL",
                Toast.LENGTH_LONG
            ).show()
            centerOn(montgomery, zoom = 13.0)
            scheduleFetch()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // osmdroid needs a unique User-Agent (use the app package name).
        Configuration.getInstance().userAgentValue = packageName
        Configuration.getInstance().load(
            applicationContext,
            getSharedPreferences("osmdroid", MODE_PRIVATE)
        )

        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        // Load local summaries from assets/summaries.json.
        // To use your own server later: implement SummarySource as RemoteSummarySource
        // and pass that into SummaryRepository instead of AssetSummarySource.
        summaryRepo = SummaryRepository(AssetSummarySource(this))
        summaryRepo.ensureLoaded()

        blueIcon = makePinDrawable(0xFF2196F3.toInt())
        goldIcon = makePinDrawable(0xFFF9A825.toInt())

        setupMap()
        binding.btnMyLocation.setOnClickListener { requestLocationOrCenter() }

        requestLocationOrCenter()
    }

    private fun setupMap() {
        val map = binding.map

        // Esri World Street Map — publicly usable tiles, no API key required.
        // URL pattern: .../tile/{z}/{y}/{x}
        val esriStreet = object : OnlineTileSourceBase(
            "EsriWorldStreetMap",
            0, 19, 256, ".jpg",
            arrayOf("https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/"),
            "© Esri"
        ) {
            override fun getTileURLString(pMapTileIndex: Long): String {
                val z = MapTileIndex.getZoom(pMapTileIndex)
                val x = MapTileIndex.getX(pMapTileIndex)
                val y = MapTileIndex.getY(pMapTileIndex)
                // Esri expects /tile/{z}/{y}/{x}
                return baseUrl + z + "/" + y + "/" + x
            }
        }

        map.setTileSource(esriStreet)
        map.setMultiTouchControls(true)
        map.zoomController.setVisibility(CustomZoomButtonsController.Visibility.SHOW_AND_FADEOUT)
        map.minZoomLevel = 4.0
        map.maxZoomLevel = 19.0

        // Dim tiles slightly optional — leave default.
        map.overlayManager.tilesOverlay.setColorFilter(null)

        // After pan/zoom settles, reload NRHP markers for the new viewport.
        map.addMapListener(object : MapListener {
            override fun onScroll(event: ScrollEvent?): Boolean {
                scheduleFetch()
                return false
            }

            override fun onZoom(event: ZoomEvent?): Boolean {
                scheduleFetch()
                return false
            }
        })

        // Clustering overlay (groups nearby pins when zoomed out).
        val cluster = RadiusMarkerClusterer(this)
        cluster.setRadius(90)
        clusterOverlay = cluster
        map.overlays.add(cluster)
    }

    private fun requestLocationOrCenter() {
        val fine = ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION)
        val coarse = ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_COARSE_LOCATION)
        if (fine == PackageManager.PERMISSION_GRANTED || coarse == PackageManager.PERMISSION_GRANTED) {
            centerOnUser()
        } else {
            permissionLauncher.launch(
                arrayOf(
                    Manifest.permission.ACCESS_FINE_LOCATION,
                    Manifest.permission.ACCESS_COARSE_LOCATION
                )
            )
        }
    }

    private fun centerOnUser() {
        val lm = getSystemService(LOCATION_SERVICE) as LocationManager
        try {
            val last = listOf(
                LocationManager.GPS_PROVIDER,
                LocationManager.NETWORK_PROVIDER,
                LocationManager.PASSIVE_PROVIDER
            ).mapNotNull { provider ->
                try {
                    if (lm.isProviderEnabled(provider)) lm.getLastKnownLocation(provider) else null
                } catch (_: SecurityException) {
                    null
                }
            }.maxByOrNull { it.time }

            if (last != null) {
                centerOn(GeoPoint(last.latitude, last.longitude), zoom = 14.0)
                scheduleFetch()
            } else {
                // Ask for a fresh fix once; meanwhile show Montgomery so the UI isn't empty.
                centerOn(montgomery, zoom = 13.0)
                scheduleFetch()
                val listener = object : LocationListener {
                    override fun onLocationChanged(location: Location) {
                        centerOn(GeoPoint(location.latitude, location.longitude), zoom = 14.0)
                        scheduleFetch()
                        try {
                            lm.removeUpdates(this)
                        } catch (_: Exception) {
                        }
                    }
                }
                try {
                    lm.requestSingleUpdate(LocationManager.NETWORK_PROVIDER, listener, Looper.getMainLooper())
                } catch (_: Exception) {
                    try {
                        lm.requestSingleUpdate(LocationManager.GPS_PROVIDER, listener, Looper.getMainLooper())
                    } catch (_: Exception) {
                        Toast.makeText(this, "Could not get GPS — using Montgomery", Toast.LENGTH_SHORT).show()
                    }
                }
            }
        } catch (e: SecurityException) {
            centerOn(montgomery, zoom = 13.0)
            scheduleFetch()
        }
    }

    private fun centerOn(point: GeoPoint, zoom: Double) {
        binding.map.controller.setZoom(zoom)
        binding.map.controller.setCenter(point)
    }

    /** Wait briefly after the last map move, then fetch places for the visible box. */
    private fun scheduleFetch() {
        fetchRunnable?.let { mainHandler.removeCallbacks(it) }
        val r = Runnable { fetchPlacesForVisibleBounds() }
        fetchRunnable = r
        mainHandler.postDelayed(r, debounceMs)
    }

    private fun fetchPlacesForVisibleBounds() {
        val box = binding.map.boundingBox ?: return
        // osmdroid BoundingBox: latNorth, latSouth, lonEast, lonWest
        val west = box.lonWest
        val south = box.latSouth
        val east = box.lonEast
        val north = box.latNorth

        // Guard against nonsense envelopes (map not ready yet).
        if (!west.isFinite() || !south.isFinite() || !east.isFinite() || !north.isFinite()) return
        if (west == east || south == north) return

        fetchJob?.cancel()
        binding.progress.visibility = View.VISIBLE

        fetchJob = scope.launch {
            val result = withContext(Dispatchers.IO) {
                runCatching { NrhpApi.fetchInBounds(west, south, east, north) }
            }
            binding.progress.visibility = View.GONE

            result.onSuccess { places ->
                showPlaces(places)
            }.onFailure { err ->
                Toast.makeText(
                    this@MainActivity,
                    "Could not load places: ${err.message}",
                    Toast.LENGTH_LONG
                ).show()
            }
        }
    }

    private fun showPlaces(places: List<NrhpPlace>) {
        val cluster = clusterOverlay ?: return
        cluster.getItems().clear()

        for (place in places) {
            val marker = Marker(binding.map)
            marker.position = GeoPoint(place.lat, place.lon)
            marker.title = place.name
            marker.snippet = place.city
            marker.icon = if (place.isNhl) goldIcon else blueIcon
            marker.setAnchor(Marker.ANCHOR_CENTER, Marker.ANCHOR_BOTTOM)
            marker.relatedObject = place
            marker.setOnMarkerClickListener { m, _ ->
                val p = m.relatedObject as? NrhpPlace
                if (p != null) showPlaceSheet(p)
                true
            }
            cluster.add(marker)
        }

        cluster.invalidate()
        binding.map.invalidate()
    }

    private fun showPlaceSheet(place: NrhpPlace) {
        val dialog = BottomSheetDialog(this)
        val sheet = BottomSheetPlaceBinding.inflate(layoutInflater)

        sheet.txtName.text = place.name
        sheet.txtAddress.text = "Address: ${place.address.ifBlank { "—" }}"
        sheet.txtCity.text = "City: ${place.city.ifBlank { "—" }}"
        sheet.txtCounty.text = "County: ${place.county.ifBlank { "—" }}"
        sheet.txtType.text = "Type: ${place.type.ifBlank { "—" }}"
        sheet.txtListed.text = "Listed: ${place.certDate.ifBlank { "—" }}"
        sheet.txtNhl.text = "NHL: ${if (place.isNhl) "yes" else "no"}"

        // History summary (bundled for ~25 Montgomery places so far).
        sheet.txtHistory.movementMethod = ScrollingMovementMethod.getInstance()
        val placeSummary = summaryRepo.summaryFor(place.refNum)
        if (placeSummary != null && placeSummary.summary.isNotBlank()) {
            sheet.txtHistoryAttribution.visibility = View.VISIBLE
            sheet.txtHistory.text = placeSummary.summary
        } else {
            // Still show the section so the user knows summaries are optional / expanding.
            sheet.txtHistoryAttribution.visibility = View.GONE
            sheet.txtHistory.text = getString(R.string.history_none)
        }

        val hasNara = place.naraUrl.startsWith("http")
        sheet.btnNomination.isEnabled = hasNara
        sheet.btnNomination.setOnClickListener {
            if (hasNara) {
                startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(place.naraUrl)))
            }
        }

        sheet.btnDirections.setOnClickListener {
            // geo: URI works with Google Maps, Organic Maps, etc.
            val geo = Uri.parse("geo:${place.lat},${place.lon}?q=${place.lat},${place.lon}(${Uri.encode(place.name)})")
            val intent = Intent(Intent.ACTION_VIEW, geo)
            if (intent.resolveActivity(packageManager) != null) {
                startActivity(intent)
            } else {
                // Fallback: open Google Maps in a browser.
                val web = Uri.parse(
                    "https://www.google.com/maps/dir/?api=1&destination=${place.lat},${place.lon}"
                )
                startActivity(Intent(Intent.ACTION_VIEW, web))
            }
        }

        sheet.btnClose.setOnClickListener { dialog.dismiss() }

        dialog.setContentView(sheet.root)
        dialog.show()
    }

    /** Draw a simple map-pin bitmap so we don't need extra image assets. */
    private fun makePinDrawable(color: Int): BitmapDrawable {
        val w = 64
        val h = 84
        val bmp = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(bmp)
        val paint = Paint(Paint.ANTI_ALIAS_FLAG).apply { this.color = color }
        val path = Path()
        // Circle head
        canvas.drawCircle(w / 2f, h * 0.38f, w * 0.28f, paint)
        // Pointy tip
        path.moveTo(w * 0.22f, h * 0.48f)
        path.lineTo(w / 2f, h * 0.95f)
        path.lineTo(w * 0.78f, h * 0.48f)
        path.close()
        canvas.drawPath(path, paint)
        // Inner white dot
        paint.color = 0xFFFFFFFF.toInt()
        canvas.drawCircle(w / 2f, h * 0.38f, w * 0.10f, paint)
        return BitmapDrawable(resources, bmp)
    }

    override fun onResume() {
        super.onResume()
        binding.map.onResume()
    }

    override fun onPause() {
        binding.map.onPause()
        super.onPause()
    }

    override fun onDestroy() {
        fetchRunnable?.let { mainHandler.removeCallbacks(it) }
        fetchJob?.cancel()
        super.onDestroy()
    }
}
