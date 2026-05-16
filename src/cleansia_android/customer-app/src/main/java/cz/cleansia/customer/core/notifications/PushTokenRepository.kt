package cz.cleansia.customer.core.notifications
import cz.cleansia.core.auth.SessionScopedCache

import android.content.Context
import android.os.Build
import android.provider.Settings
import android.util.Log
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.google.firebase.messaging.FirebaseMessaging
import cz.cleansia.core.network.networkCall
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

private val Context.pushTokenDataStore by preferencesDataStore(name = "push_token_state")

/**
 * Owns the FCM token lifecycle for the signed-in user.
 *
 * Three responsibilities:
 *  1. Ask FCM for the current token on demand (sign-in, settings change).
 *  2. POST it to /api/Device/Register when it changes (and only then).
 *  3. DELETE it on sign-out so the next user on this handset doesn't
 *     inherit notifications for the previous one.
 *
 * Persists the last-registered token in DataStore so we don't re-POST on
 * every cold start. The messaging service's `onNewToken` handles rotation
 * out-of-band; this repo handles the explicit register/unregister cases.
 */
@Singleton
class PushTokenRepository @Inject constructor(
    private val deviceApi: DeviceApi,
    @ApplicationContext private val context: Context,
) : cz.cleansia.core.auth.SessionScopedCache {

    /**
     * SessionScopedCache contract — clears the locally-cached "last
     * registered token" so the next user on this handset re-registers
     * fresh on their first login. Does NOT call the network unregister
     * endpoint (forced sign-out happens AFTER the access token is dead,
     * so the call would 401 anyway). User-initiated logout in
     * AuthRepository.logout calls [unregisterDevice] explicitly to also
     * delete the row server-side.
     */
    override suspend fun clear() {
        clearLastRegisteredToken()
    }

    private val deviceId: String by lazy { resolveDeviceId(context) }

    /**
     * Fetch the current FCM token (auto-creating one on first call), then
     * POST it to the backend. Idempotent — backend upserts on
     * (UserId, DeviceId). Skips the network round-trip when the token
     * hasn't changed since the last successful registration.
     *
     * Caller is the auth flow (sign-in success). Safe to call on every
     * sign-in: if the token hasn't rotated and we already POSTed it,
     * we no-op locally.
     */
    suspend fun registerCurrentToken() {
        val token = fetchTokenOrNull() ?: return
        val lastRegistered = readLastRegisteredToken()
        if (token == lastRegistered) return

        val response = networkCall(TAG) {
            deviceApi.register(
                RegisterDeviceRequest(
                    deviceId = deviceId,
                    deviceToken = token,
                    platform = "android",
                ),
            )
        }
        if (response?.isSuccessful == true) {
            writeLastRegisteredToken(token)
        }
    }

    /**
     * Forced overwrite of the last-registered token + immediate POST.
     * Used by the FirebaseMessagingService.onNewToken hook when FCM
     * rotates the token while the app is running. Bypasses the
     * "unchanged" short-circuit because the input is the new value.
     */
    suspend fun onTokenRotated(newToken: String) {
        val response = networkCall(TAG) {
            deviceApi.register(
                RegisterDeviceRequest(
                    deviceId = deviceId,
                    deviceToken = newToken,
                    platform = "android",
                ),
            )
        }
        if (response?.isSuccessful == true) {
            writeLastRegisteredToken(newToken)
        }
    }

    /**
     * Tear down the device row server-side and clear local state. MUST
     * run before the access token is wiped (the API call needs the JWT).
     * Called from the logout flow.
     */
    suspend fun unregisterDevice() {
        networkCall(TAG) { deviceApi.unregister(deviceId) }
        clearLastRegisteredToken()
    }

    private suspend fun fetchTokenOrNull(): String? {
        // Wrap the Play Services Task in a coroutine. We don't depend on
        // kotlinx-coroutines-play-services so that this module stays
        // independent of an extra Google Play Services artifact.
        return try {
            suspendCancellableCoroutine { cont ->
                FirebaseMessaging.getInstance().token
                    .addOnSuccessListener { cont.resume(it) }
                    .addOnFailureListener { cont.resumeWithException(it) }
            }
        } catch (ce: CancellationException) {
            throw ce
        } catch (t: Throwable) {
            Log.w(TAG, "Failed to fetch FCM token: ${t.message}")
            null
        }
    }

    private suspend fun readLastRegisteredToken(): String? {
        val prefs = context.pushTokenDataStore.data.first()
        return prefs[KEY_LAST_TOKEN]
    }

    private suspend fun writeLastRegisteredToken(token: String) {
        context.pushTokenDataStore.edit { it[KEY_LAST_TOKEN] = token }
    }

    private suspend fun clearLastRegisteredToken() {
        context.pushTokenDataStore.edit { it.remove(KEY_LAST_TOKEN) }
    }

    /**
     * Stable per-install device id. ANDROID_ID is reset on factory reset
     * + per-app on Android 8+, which is the right granularity here — a
     * factory reset SHOULD invalidate the FCM registration anyway, and
     * ANDROID_ID being app-scoped means two apps on the same device see
     * different ids (so the partner app's device row doesn't collide).
     */
    private fun resolveDeviceId(context: Context): String {
        val android = Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID)
        if (!android.isNullOrEmpty()) return android
        // Rare nulls — fall back to a build-derived synthetic. SERIAL was
        // deprecated in API 26; we deliberately avoid it because the
        // permission story would force REQUEST_INSTALL_PACKAGES.
        return "${Build.MANUFACTURER}-${Build.MODEL}"
    }

    private companion object {
        const val TAG = "PushTokenRepository"
        val KEY_LAST_TOKEN = stringPreferencesKey("last_registered_token")
    }
}
