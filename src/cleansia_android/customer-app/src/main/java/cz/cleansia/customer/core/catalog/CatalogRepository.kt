package cz.cleansia.customer.core.catalog

import android.content.Context
import android.util.Log
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * In-memory cache of the public services + packages catalog. Screens observe
 * [services] and [packages]; call [refresh] on startup / when entering booking.
 *
 * Category derivation (distinct by slug, sorted by displayOrder) lives at the
 * call site — the services flow already carries the data and adding a second
 * derived StateFlow here would just duplicate reactivity.
 *
 * [refresh] returns [ApiResult.Success] once the catalog is warm and
 * [ApiResult.Error] carrying the parsed message on failure. The consuming
 * ViewModel surfaces the snackbar; an [ApiError.Network] failure stays silent
 * (NetworkErrorInterceptor owns the infra toast).
 */
@Singleton
class CatalogRepository @Inject constructor(
    private val api: CatalogApi,
    @ApplicationContext private val appContext: Context,
) {
    private val _services = MutableStateFlow<List<ServiceListItem>>(emptyList())
    private val _packages = MutableStateFlow<List<PackageListItem>>(emptyList())
    private val _extras = MutableStateFlow<List<ExtraListItem>>(emptyList())
    private val _loading = MutableStateFlow(false)
    private val _loaded = MutableStateFlow(false)

    val services: StateFlow<List<ServiceListItem>> = _services.asStateFlow()
    val packages: StateFlow<List<PackageListItem>> = _packages.asStateFlow()
    val extras: StateFlow<List<ExtraListItem>> = _extras.asStateFlow()
    val loading: StateFlow<Boolean> = _loading.asStateFlow()
    val loaded: StateFlow<Boolean> = _loaded.asStateFlow()

    suspend fun refresh(): ApiResult<Unit> {
        if (_loading.value) {
            Log.d(TAG, "refresh: already loading, skipping")
            return ApiResult.Success(Unit)
        }
        _loading.value = true
        Log.d(TAG, "refresh: start")
        try {
            val servicesResp = networkCall(TAG) { api.getServices() }
                ?: return networkError()

            val packagesResp = networkCall(TAG) { api.getPackages() }
                ?: return networkError()

            // Extras are best-effort — if the call fails (e.g. backend rolled
            // back before the Extras table shipped to this env) the wizard
            // still functions, just without the add-on section. Don't fail
            // the whole catalog refresh on this one.
            val extrasResp = networkCall(TAG) { api.getExtras() }

            Log.d(TAG, "refresh: services http=${servicesResp.code()} ok=${servicesResp.isSuccessful}")
            Log.d(TAG, "refresh: packages http=${packagesResp.code()} ok=${packagesResp.isSuccessful}")
            Log.d(TAG, "refresh: extras http=${extrasResp?.code()} ok=${extrasResp?.isSuccessful}")

            if (!servicesResp.isSuccessful) {
                return httpError(servicesResp.errorBody(), servicesResp.code())
            }

            if (!packagesResp.isSuccessful) {
                return httpError(packagesResp.errorBody(), packagesResp.code())
            }

            val servicesBody = servicesResp.body()
            val packagesBody = packagesResp.body()
            val extrasBody = if (extrasResp?.isSuccessful == true) extrasResp.body() else null
            Log.d(TAG, "refresh: parsed servicesBody=${servicesBody?.size} packagesBody=${packagesBody?.size} extrasBody=${extrasBody?.size}")

            _services.value = servicesBody.orEmpty()
            _packages.value = packagesBody.orEmpty()
            _extras.value = extrasBody.orEmpty()
            _loaded.value = true
            Log.d(TAG, "refresh: DONE, _services=${_services.value.size} _packages=${_packages.value.size} _extras=${_extras.value.size} _loaded=true")
            return ApiResult.Success(Unit)
        } finally {
            _loading.value = false
        }
    }

    private fun networkError(): ApiResult<Unit> =
        ApiResult.Error(ApiError.Network(appContext.getString(R.string.error_generic_network)))

    private fun httpError(errorBody: okhttp3.ResponseBody?, httpCode: Int): ApiResult<Unit> {
        // Carry the message [ApiErrorParser] already resolved from the body so
        // the surfacing ViewModel shows the identical string. The 401 object
        // would drop that message, so it folds into the message-carrying
        // [ApiError.Unknown] alongside the generic fallback.
        val message = ApiErrorParser.parseToUserMessage(appContext, errorBody, httpCode)
        val error = when (httpCode) {
            404 -> ApiError.NotFound(message)
            400 -> ApiError.BadRequest(message)
            in 500..599 -> ApiError.Server(statusCode = httpCode, message = message)
            else -> ApiError.Unknown(message)
        }
        return ApiResult.Error(error)
    }

    private companion object {
        const val TAG = "CatalogRepository"
    }
}
