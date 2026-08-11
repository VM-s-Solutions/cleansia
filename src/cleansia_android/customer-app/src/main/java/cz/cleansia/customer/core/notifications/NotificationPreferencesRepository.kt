package cz.cleansia.customer.core.notifications

import android.content.Context
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

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
    @ApplicationContext private val appContext: Context,
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
            call { api.getMine() }.onSuccess { _preferences.value = it }
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
        return call { api.update(payload) }
            .onSuccess { _preferences.value = it }
            .onError { _preferences.value = previous }
    }

    /**
     * Reads the body explicitly rather than through `safeApiCall`, which answers a 2xx-with-no-body
     * with `Success(Unit as T)` — an unchecked cast that would store `Unit` in the snapshot and
     * throw at the first read. [NotificationPreferencesApi] answers a contract refusal with exactly
     * that shape.
     */
    private suspend fun call(
        block: suspend () -> retrofit2.Response<NotificationPreferencesPayload>,
    ): ApiResult<NotificationPreferencesPayload> {
        val resp = networkCall { block() } ?: return networkError()
        if (!resp.isSuccessful) return httpError(resp.errorBody(), resp.code())
        return resp.body()?.let { ApiResult.Success(it) } ?: emptyBodyError()
    }

    private fun networkError(): ApiResult<Nothing> =
        ApiResult.Error(ApiError.Network(appContext.getString(R.string.error_generic_network)))

    /**
     * A 2xx whose body did not survive [NotificationPreferencesApi]'s contract refusal. Deliberately
     * not [ApiError.Network]: that channel is the silent one and the network is the one thing that
     * did not fail.
     */
    private fun emptyBodyError(): ApiResult<Nothing> =
        ApiResult.Error(ApiError.Unknown(appContext.getString(R.string.error_generic_unknown)))

    private fun httpError(errorBody: okhttp3.ResponseBody?, httpCode: Int): ApiResult<Nothing> {
        val message = ApiErrorParser.parseToUserMessage(appContext, errorBody, httpCode)
        val error = when (httpCode) {
            404 -> ApiError.NotFound(message)
            400 -> ApiError.BadRequest(message)
            in 500..599 -> ApiError.Server(statusCode = httpCode, message = message)
            else -> ApiError.Unknown(message)
        }
        return ApiResult.Error(error)
    }
}
