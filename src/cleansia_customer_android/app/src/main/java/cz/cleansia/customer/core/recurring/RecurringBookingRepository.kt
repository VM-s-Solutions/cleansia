package cz.cleansia.customer.core.recurring

import android.util.Log
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Cache + orchestrator for the user's recurring booking templates. Same
 * swallow-and-log pattern as the other customer repos: foreground operations
 * return null on failure (caller surfaces a snackbar), the cached
 * [templates] flow stays as-is so the UI keeps rendering.
 */
@Singleton
class RecurringBookingRepository @Inject constructor(
    private val api: RecurringBookingApi,
) {
    private val mutex = Mutex()

    private val _templates = MutableStateFlow<List<RecurringBookingTemplateDto>>(emptyList())
    val templates: StateFlow<List<RecurringBookingTemplateDto>> = _templates.asStateFlow()

    private val _loading = MutableStateFlow(false)
    val loading: StateFlow<Boolean> = _loading.asStateFlow()

    private val _loaded = MutableStateFlow(false)
    val loaded: StateFlow<Boolean> = _loaded.asStateFlow()

    suspend fun refresh(): List<RecurringBookingTemplateDto>? = mutex.withLock {
        _loading.value = true
        try {
            val response = runCatching { api.getMine() }.getOrNull()
            if (response?.isSuccessful == true) {
                val body = response.body() ?: emptyList()
                _templates.value = body
                _loaded.value = true
                return@withLock body
            }
            Log.w(TAG, "getMine failed: HTTP ${response?.code()}")
            return@withLock null
        } finally {
            _loading.value = false
        }
    }

    suspend fun create(request: CreateRecurringBookingRequest): RecurringBookingTemplateDto? {
        val resp = runWithLog("create") { api.create(request) }
        if (resp != null) refresh()
        return resp
    }

    suspend fun update(request: UpdateRecurringBookingRequest): RecurringBookingTemplateDto? {
        val resp = runWithLog("update") { api.update(request) }
        if (resp != null) refresh()
        return resp
    }

    suspend fun setActive(templateId: String, isActive: Boolean): Boolean {
        val resp = runCatching {
            api.setActive(SetRecurringBookingActiveRequest(templateId, isActive))
        }.getOrNull()
        val ok = resp?.isSuccessful == true
        if (ok) refresh()
        return ok
    }

    suspend fun delete(templateId: String): Boolean {
        val resp = runCatching {
            api.delete(DeleteRecurringBookingRequest(templateId))
        }.getOrNull()
        val ok = resp?.isSuccessful == true
        if (ok) refresh()
        return ok
    }

    fun clear() {
        _templates.value = emptyList()
        _loaded.value = false
    }

    private suspend inline fun <T> runWithLog(
        label: String,
        block: () -> retrofit2.Response<T>,
    ): T? {
        return try {
            val response = block()
            if (response.isSuccessful) {
                response.body()
            } else {
                Log.w(TAG, "$label failed: HTTP ${response.code()}")
                null
            }
        } catch (t: Throwable) {
            Log.w(TAG, "$label threw", t)
            null
        }
    }

    private companion object {
        const val TAG = "RecurringBookingRepo"
    }
}
