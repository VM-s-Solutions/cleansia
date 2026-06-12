package cz.cleansia.partner.core.notifications

import android.content.Context
import android.util.Log
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.google.firebase.messaging.FirebaseMessaging
import cz.cleansia.core.auth.DeviceIdProvider
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.safeApiCall
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

private val Context.pushTokenDataStore by preferencesDataStore(name = "partner_push_token_state")

/**
 * Owns the FCM token lifecycle for the signed-in cleaner. Mirrors the
 * customer-app repository, adapted to partner's [safeApiCall]/[ApiResult]
 * boundary (the customer side uses :core `networkCall`).
 *
 * Three responsibilities:
 *  1. Ask FCM for the current token on demand (sign-in).
 *  2. POST it to /api/Device/Register when it changes (and only then).
 *  3. DELETE it on sign-out so the next user on this handset doesn't inherit
 *     notifications for the previous one.
 *
 * Persists the last-registered token in DataStore so we don't re-POST on every
 * cold start. The messaging service's `onNewToken` handles rotation
 * out-of-band; this repo handles the explicit register/unregister cases.
 */
@Singleton
class PushTokenRepository @Inject constructor(
    private val deviceApi: DeviceApi,
    private val json: Json,
    deviceIdProvider: DeviceIdProvider,
    @ApplicationContext private val context: Context,
) : SessionScopedCache {

    private val deviceId: String by lazy { deviceIdProvider.deviceId }

    /**
     * Latest FCM token observed via either the initial fetch
     * ([fetchAndCacheCurrentToken]) or the messaging service's onNewToken
     * callback ([reportRotatedToken]). Null until the first emission.
     *
     * Exposed as a hot flow so [PushTokenSessionObserver] can react both
     * to the initial fetch and to mid-session rotations without juggling
     * Firebase callbacks itself.
     */
    val fcmToken: StateFlow<String?> get() = _fcmToken.asStateFlow()
    private val _fcmToken = MutableStateFlow<String?>(null)

    /**
     * SessionScopedCache contract — clears the locally-cached "last registered
     * token" so the next user on this handset re-registers fresh on their first
     * login. Does NOT hit the network (forced sign-out happens after the access
     * token is dead, so the call would 401 anyway). User-initiated logout calls
     * [unregisterDevice] explicitly to also delete the row server-side.
     */
    override suspend fun clear() {
        clearLastRegisteredToken()
    }

    /**
     * Asks FCM for the current token and pushes it into [fcmToken]. Called
     * once by the session observer on cold start — without this the
     * [fcmToken] flow would stay null until FCM happened to rotate (which
     * may never happen for the lifetime of an install).
     *
     * Runs the one-shot Firebase-project migration first so installs that
     * have a cached token from the previous (wrong) Firebase project get
     * a fresh token from the new project on this launch instead of
     * waiting weeks for spontaneous FCM rotation.
     */
    suspend fun fetchAndCacheCurrentToken() {
        runFirebaseProjectMigrationOnce()
        fetchTokenOrNull()?.let { _fcmToken.value = it }
    }

    /**
     * One-shot reset of the FCM token + the local "last registered"
     * cache. Necessary after a `google-services.json` swap that changes
     * the Firebase project: FCM caches the token from the OLD project
     * forever until either an explicit `deleteToken()` or spontaneous
     * rotation, and our DataStore cache short-circuits the backend
     * re-register if the token string hasn't changed.
     *
     * Gated by a DataStore key so it runs exactly once per install.
     * Bumping [MIGRATION_VERSION] forces it to run again on next launch
     * if we ever do another project swap.
     */
    private suspend fun runFirebaseProjectMigrationOnce() {
        val prefs = context.pushTokenDataStore.data.first()
        if (prefs[KEY_MIGRATION_VERSION] == MIGRATION_VERSION) return
        runCatching {
            suspendCancellableCoroutine { cont ->
                FirebaseMessaging.getInstance().deleteToken()
                    .addOnSuccessListener { cont.resume(Unit) }
                    .addOnFailureListener { cont.resumeWithException(it) }
            }
        }.onFailure { Log.w(TAG, "FCM deleteToken during migration failed: ${it.message}") }
        clearLastRegisteredToken()
        context.pushTokenDataStore.edit { it[KEY_MIGRATION_VERSION] = MIGRATION_VERSION }
        Log.i(TAG, "Ran FCM token reset migration v$MIGRATION_VERSION")
    }

    /**
     * Called by the messaging service when FCM rotates the token. Updates
     * the in-memory flow; the session observer notices the change and
     * re-registers if a session is active. Replaces the previous direct
     * network call which 401'd when the user wasn't signed in.
     */
    fun reportRotatedToken(token: String) {
        _fcmToken.value = token
    }

    /**
     * Register [token] with the backend if it differs from the last
     * successfully-registered one. Backend upserts on (UserId, DeviceId)
     * so re-registering the same token is a no-op server-side; the local
     * cache just saves the round-trip. Driven by [PushTokenSessionObserver]
     * whenever an auth session is active and a token is available.
     */
    suspend fun ensureRegistered(token: String) {
        if (token == readLastRegisteredToken()) return
        postToken(token)
    }

    /**
     * Tear down the device row server-side and clear local state. MUST run
     * before the access token is wiped (the API call needs the JWT). Called
     * from the logout flow.
     */
    suspend fun unregisterDevice() {
        safeApiCall(json) { deviceApi.unregister(deviceId) }
        clearLastRegisteredToken()
    }

    private suspend fun postToken(token: String) {
        val result = safeApiCall(json) {
            deviceApi.register(
                RegisterDeviceRequest(
                    deviceId = deviceId,
                    deviceToken = token,
                    platform = "android",
                ),
            )
        }
        if (result is ApiResult.Success) writeLastRegisteredToken(token)
    }

    private suspend fun fetchTokenOrNull(): String? {
        // Wrap the Play Services Task in a coroutine. We don't depend on
        // kotlinx-coroutines-play-services so the module stays independent of
        // an extra Google Play Services artifact.
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

    private suspend fun readLastRegisteredToken(): String? =
        context.pushTokenDataStore.data.first()[KEY_LAST_TOKEN]

    private suspend fun writeLastRegisteredToken(token: String) {
        context.pushTokenDataStore.edit { it[KEY_LAST_TOKEN] = token }
    }

    private suspend fun clearLastRegisteredToken() {
        context.pushTokenDataStore.edit { it.remove(KEY_LAST_TOKEN) }
    }

    private companion object {
        const val TAG = "PushTokenRepository"
        val KEY_LAST_TOKEN = stringPreferencesKey("last_registered_token")

        // One-shot migrations applied lazily on first call to
        // fetchAndCacheCurrentToken. Bump MIGRATION_VERSION whenever a
        // future change requires every install to throw away its cached
        // FCM token + re-register (e.g. another Firebase project swap).
        //  v1 (2026-05-30): partner app moved from `cleansia-28fbc` to
        //  the shared `cleansia` Firebase project. Existing installs had
        //  tokens minted by the old project; left alone they'd keep
        //  failing dispatch with SenderIdMismatch indefinitely.
        const val MIGRATION_VERSION = 1
        val KEY_MIGRATION_VERSION = intPreferencesKey("fcm_token_migration_version")
    }
}
