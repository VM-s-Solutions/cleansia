package cz.cleansia.customer.core.location

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import androidx.core.content.ContextCompat
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.tasks.CancellationTokenSource
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.coroutines.resume

data class UserLocation(val latitude: Double, val longitude: Double)

/**
 * Thin wrapper over FusedLocationProvider. Returns a single last-known location.
 * Does NOT handle permission requests — that belongs in the UI layer where we can
 * show a rationale and route to Settings on deny.
 *
 * Fails silently (returns null) if permission not granted. Callers should check
 * permission state first via [hasLocationPermission].
 */
class LocationService(
    private val context: Context,
) {
    private val fused by lazy { LocationServices.getFusedLocationProviderClient(context) }

    fun hasLocationPermission(): Boolean {
        val fine = ContextCompat.checkSelfPermission(
            context, Manifest.permission.ACCESS_FINE_LOCATION,
        ) == PackageManager.PERMISSION_GRANTED
        val coarse = ContextCompat.checkSelfPermission(
            context, Manifest.permission.ACCESS_COARSE_LOCATION,
        ) == PackageManager.PERMISSION_GRANTED
        return fine || coarse
    }

    /**
     * Get a single current location reading. Returns null if permission not granted,
     * location services disabled, or request times out.
     */
    @SuppressLint("MissingPermission") // Callers must check hasLocationPermission() first
    suspend fun getCurrentLocation(): UserLocation? {
        if (!hasLocationPermission()) return null

        return suspendCancellableCoroutine { cont ->
            val cts = CancellationTokenSource()
            cont.invokeOnCancellation { cts.cancel() }

            // HIGH_ACCURACY because the user is actively asking for their
            // location on the address picker — a 30-50m fix isn't useful for
            // confirming a street address. Also dramatically improves the
            // emulator success rate (BALANCED can return null when there's no
            // recent cached fix).
            fused.getCurrentLocation(Priority.PRIORITY_HIGH_ACCURACY, cts.token)
                .addOnSuccessListener { location ->
                    if (location != null) {
                        cont.resume(UserLocation(location.latitude, location.longitude))
                    } else {
                        cont.resume(null)
                    }
                }
                .addOnFailureListener { cont.resume(null) }
        }
    }
}
