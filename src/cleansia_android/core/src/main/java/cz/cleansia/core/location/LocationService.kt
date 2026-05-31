package cz.cleansia.core.location

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import androidx.core.content.ContextCompat
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.tasks.CancellationTokenSource
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withTimeoutOrNull
import kotlin.coroutines.resume

/**
 * Thin wrapper over FusedLocationProvider with three layers of fallback
 * so it stays useful on emulators (where FLP often returns null even
 * with a mock location set):
 *
 *  1. `getCurrentLocation(HIGH_ACCURACY)` with a 3s timeout — produces
 *     a fresh fix when the device can give one.
 *  2. `lastLocation` — FLP's last-cached fix.
 *  3. `LocationManager.getLastKnownLocation(...)` across all enabled
 *     providers — bypasses FLP's caching when the emulator has stuck
 *     mock state that never propagates to Play Services.
 *
 * Returns null when all three layers fail. Permission checks happen up
 * front; UI layer should request permission before calling.
 *
 * **Optional DEBUG stub:** when [debugFallbackLocation] is non-null and
 * `BuildConfig.DEBUG` is true at the call site, [getCurrentLocation]
 * returns it as a last resort. Used by both apps' picker dev loop to
 * keep working on broken emulator images.
 */
class LocationService(
    private val context: Context,
    private val debugFallbackLocation: UserLocation? = null,
) {
    private val fused by lazy { LocationServices.getFusedLocationProviderClient(context) }

    fun hasPermission(): Boolean {
        val fine = ContextCompat.checkSelfPermission(
            context, Manifest.permission.ACCESS_FINE_LOCATION,
        ) == PackageManager.PERMISSION_GRANTED
        val coarse = ContextCompat.checkSelfPermission(
            context, Manifest.permission.ACCESS_COARSE_LOCATION,
        ) == PackageManager.PERMISSION_GRANTED
        return fine || coarse
    }

    /**
     * Get a single current location reading. Returns null if permission
     * not granted, location services disabled, all 3 fallback layers
     * fail to produce a fix, AND no debug stub configured.
     */
    @SuppressLint("MissingPermission") // Callers must check hasPermission() first
    suspend fun getCurrentLocation(): UserLocation? {
        if (!hasPermission()) return null

        // Layer 1: ask FLP for a fresh fix with a 3s timeout. HIGH_ACCURACY
        // consults the GPS provider directly (what the emulator mocks);
        // BALANCED_POWER prefers cell/wifi which don't exist on AVDs.
        // The 3 s timeout matters because `getCurrentLocation` can hang
        // indefinitely on devices that can't produce a fresh fix.
        val fresh = withTimeoutOrNull(3000L) {
            suspendCancellableCoroutine<UserLocation?> { cont ->
                val cts = CancellationTokenSource()
                fused.getCurrentLocation(Priority.PRIORITY_HIGH_ACCURACY, cts.token)
                    .addOnSuccessListener { loc ->
                        if (cont.isActive) cont.resume(loc?.let { UserLocation(it.latitude, it.longitude) })
                    }
                    .addOnFailureListener {
                        if (cont.isActive) cont.resume(null)
                    }
                cont.invokeOnCancellation { cts.cancel() }
            }
        }

        // Layer 2: FLP's last-cached location.
        val cached = fresh ?: suspendCancellableCoroutine<UserLocation?> { cont ->
            fused.lastLocation
                .addOnSuccessListener { loc ->
                    if (cont.isActive) cont.resume(loc?.let { UserLocation(it.latitude, it.longitude) })
                }
                .addOnFailureListener {
                    if (cont.isActive) cont.resume(null)
                }
        }

        // Layer 3: ask Android's LocationManager directly. FLP via Play
        // Services often returns null on emulators even when a mock
        // location is set; LocationManager reads the mock fix directly.
        // Harmless on real devices — they'll usually have already
        // resolved above.
        val resolved = cached ?: runCatching {
            val lm = context.getSystemService(Context.LOCATION_SERVICE) as android.location.LocationManager
            lm.getProviders(true)
                .mapNotNull { p ->
                    @Suppress("MissingPermission")
                    lm.getLastKnownLocation(p)
                }
                .maxByOrNull { it.time }
                ?.let { UserLocation(it.latitude, it.longitude) }
        }.getOrNull()

        // Layer 4 (debug only): pass-through fallback set by the
        // consumer-app's Hilt module. Lets dev loops keep working on
        // emulator images where Play Services never delivers a fix even
        // when one is configured. Production apps pass null here.
        return resolved ?: debugFallbackLocation
    }
}
