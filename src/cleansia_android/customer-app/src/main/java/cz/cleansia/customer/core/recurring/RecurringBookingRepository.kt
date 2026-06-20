package cz.cleansia.customer.core.recurring
import cz.cleansia.core.auth.SessionScopedCache

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Cache + orchestrator for the user's recurring booking templates.
 *
 * Error model mirrors [cz.cleansia.customer.core.orders.OrderRepository]:
 * operations return [ApiResult.Success] on success and [ApiResult.Error]
 * carrying the parsed message on failure; the consuming ViewModel surfaces the
 * snackbar. An [ApiError.Network] failure stays silent (NetworkErrorInterceptor
 * owns the infra toast). The cached [templates] flow stays as-is on failure so
 * the UI keeps rendering.
 */
@Singleton
class RecurringBookingRepository @Inject constructor(
    private val api: RecurringBookingApi,
    @ApplicationContext private val appContext: Context,
) : cz.cleansia.core.auth.SessionScopedCache {
    private val mutex = Mutex()

    private val _templates = MutableStateFlow<List<RecurringBookingTemplateDto>>(emptyList())
    val templates: StateFlow<List<RecurringBookingTemplateDto>> = _templates.asStateFlow()

    private val _loading = MutableStateFlow(false)
    val loading: StateFlow<Boolean> = _loading.asStateFlow()

    private val _loaded = MutableStateFlow(false)
    val loaded: StateFlow<Boolean> = _loaded.asStateFlow()

    suspend fun refresh(): ApiResult<Unit> = mutex.withLock {
        _loading.value = true
        try {
            val resp = networkCall { api.getMine() } ?: return@withLock networkError()
            if (!resp.isSuccessful) {
                return@withLock httpError(resp.errorBody(), resp.code())
            }
            _templates.value = resp.body() ?: emptyList()
            _loaded.value = true
            return@withLock ApiResult.Success(Unit)
        } finally {
            _loading.value = false
        }
    }

    suspend fun create(request: CreateRecurringBookingRequest): ApiResult<RecurringBookingTemplateDto> {
        val resp = networkCall("create") { api.create(request) } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        val body = resp.body() ?: return networkError()
        refresh()
        return ApiResult.Success(body)
    }

    suspend fun update(request: UpdateRecurringBookingRequest): ApiResult<RecurringBookingTemplateDto> {
        val resp = networkCall("update") { api.update(request) } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        val body = resp.body() ?: return networkError()
        refresh()
        return ApiResult.Success(body)
    }

    suspend fun setActive(templateId: String, isActive: Boolean): ApiResult<Unit> {
        val resp = networkCall {
            api.setActive(SetRecurringBookingActiveRequest(templateId, isActive))
        } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        refresh()
        return ApiResult.Success(Unit)
    }

    suspend fun delete(templateId: String): ApiResult<Unit> {
        val resp = networkCall {
            api.delete(DeleteRecurringBookingRequest(templateId))
        } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        refresh()
        return ApiResult.Success(Unit)
    }

    override suspend fun clear() {
        _templates.value = emptyList()
        _loaded.value = false
    }

    private fun networkError(): ApiResult<Nothing> =
        ApiResult.Error(ApiError.Network(appContext.getString(R.string.error_generic_network)))

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
