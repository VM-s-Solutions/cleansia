package cz.cleansia.customer.core.notifications
import cz.cleansia.core.auth.SessionScopedCache

import cz.cleansia.core.network.networkCall
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Thin façade over [NotificationPreferencesApi]. The backend contract is
 * upsert-on-read + replace-all on write, so the repo is essentially a
 * snapshot cache of the most recent payload — fetch once on screen open,
 * write the full payload on each toggle change.
 *
 * No SessionScopedCache — preferences are user-scoped state but the
 * snapshot cache is one row deep and has no security risk if it leaks
 * across users on the same device (next user's GET overwrites it).
 */
@Singleton
class NotificationPreferencesRepository @Inject constructor(
    private val api: NotificationPreferencesApi,
) {
    private val _preferences = MutableStateFlow<NotificationPreferencesPayload?>(null)
    val preferences: StateFlow<NotificationPreferencesPayload?> = _preferences.asStateFlow()

    private val _loading = MutableStateFlow(false)
    val loading: StateFlow<Boolean> = _loading.asStateFlow()

    /**
     * Fetch from the server. Lazy-creates the row backend-side if missing,
     * so the response always shapes the full DTO.
     *
     * @return null on success, error message string on failure (snackbar
     *   surfaced by the calling VM).
     */
    suspend fun refresh(): NotificationPreferencesPayload? {
        if (_loading.value) return _preferences.value
        _loading.value = true
        try {
            val response = networkCall(TAG) { api.getMine() }
            if (response?.isSuccessful == true) {
                _preferences.value = response.body()
            }
            return _preferences.value
        } finally {
            _loading.value = false
        }
    }

    /**
     * Replace-all PUT. Updates the local snapshot optimistically before
     * the network call so the UI feels instant; reverts on failure.
     */
    suspend fun update(payload: NotificationPreferencesPayload): Boolean {
        val previous = _preferences.value
        _preferences.value = payload
        val response = networkCall(TAG) { api.update(payload) }
        if (response?.isSuccessful == true) {
            _preferences.value = response.body() ?: payload
            return true
        }
        _preferences.value = previous
        return false
    }

    private companion object {
        const val TAG = "NotificationPreferencesRepository"
    }
}
