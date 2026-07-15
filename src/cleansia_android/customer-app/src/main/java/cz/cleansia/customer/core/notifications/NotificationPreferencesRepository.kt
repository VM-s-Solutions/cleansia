package cz.cleansia.customer.core.notifications

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.serialization.json.Json

/**
 * Thin facade over [NotificationPreferencesApi]. The backend contract is
 * upsert-on-read + replace-all on write, so the repo is essentially a
 * snapshot cache of the most recent payload - fetch once on screen open,
 * write the full payload on each toggle change.
 *
 * Per-user snapshot, so it joins the [SessionScopedCache] multibinding: without
 * the wipe the next account on a shared device briefly sees the prior user's
 * toggles, and a quick toggle would race the prior payload into a replace-all
 * PUT under the new user's session.
 */
@Singleton
class NotificationPreferencesRepository @Inject constructor(
    private val api: NotificationPreferencesApi,
    private val json: Json,
) : SessionScopedCache {
    private val _preferences = MutableStateFlow<NotificationPreferencesPayload?>(null)
    val preferences: StateFlow<NotificationPreferencesPayload?> = _preferences.asStateFlow()

    private val _loading = MutableStateFlow(false)
    val loading: StateFlow<Boolean> = _loading.asStateFlow()

    override suspend fun clear() {
        _preferences.value = null
        _loading.value = false
    }

    /**
     * Fetch from the server. Lazy-creates the row backend-side if missing,
     * so the response always shapes the full DTO. The snapshot is left
     * untouched on failure; the calling VM keeps this path silent.
     */
    suspend fun refresh(): ApiResult<NotificationPreferencesPayload> {
        _preferences.value?.let { if (_loading.value) return ApiResult.Success(it) }
        _loading.value = true
        return try {
            safeApiCall(json) { api.getMine() }
                .onSuccess { _preferences.value = it }
        } finally {
            _loading.value = false
        }
    }

    /**
     * Replace-all PUT. Updates the local snapshot optimistically before the
     * network call so the UI feels instant; reverts on failure.
     */
    suspend fun update(payload: NotificationPreferencesPayload): ApiResult<NotificationPreferencesPayload> {
        val previous = _preferences.value
        _preferences.value = payload
        return safeApiCall(json) { api.update(payload) }
            .onSuccess { _preferences.value = it }
            .onError { _preferences.value = previous }
    }
}
