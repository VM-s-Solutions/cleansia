package cz.cleansia.customer.core.catalog

import android.content.Context
import android.util.Log
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.network.networkCall
import cz.cleansia.core.snackbar.SnackbarController
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
 * Mutation-style [refresh] returns null on success or a translated user-facing
 * error message on failure, mirroring [UserRepository]/[AddressRepository].
 * Failures also push a snackbar so fire-and-forget callers still surface them.
 */
@Singleton
class CatalogRepository @Inject constructor(
    private val api: CatalogApi,
    private val snackbar: SnackbarController,
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

    suspend fun refresh(): String? {
        if (_loading.value) {
            Log.d(TAG, "refresh: already loading, skipping")
            return null
        }
        _loading.value = true
        Log.d(TAG, "refresh: start")
        try {
            val servicesResp = networkCall(TAG) { api.getServices() }
                ?: return appContext.getString(R.string.error_generic_network)

            val packagesResp = networkCall(TAG) { api.getPackages() }
                ?: return appContext.getString(R.string.error_generic_network)

            // Extras are best-effort — if the call fails (e.g. backend rolled
            // back before the Extras table shipped to this env) the wizard
            // still functions, just without the add-on section. Don't fail
            // the whole catalog refresh on this one.
            val extrasResp = networkCall(TAG) { api.getExtras() }

            Log.d(TAG, "refresh: services http=${servicesResp.code()} ok=${servicesResp.isSuccessful}")
            Log.d(TAG, "refresh: packages http=${packagesResp.code()} ok=${packagesResp.isSuccessful}")
            Log.d(TAG, "refresh: extras http=${extrasResp?.code()} ok=${extrasResp?.isSuccessful}")

            if (!servicesResp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(
                    appContext,
                    servicesResp.errorBody(),
                    servicesResp.code(),
                )
                snackbar.showError(msg)
                return msg
            }

            if (!packagesResp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(
                    appContext,
                    packagesResp.errorBody(),
                    packagesResp.code(),
                )
                snackbar.showError(msg)
                return msg
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
            return null
        } finally {
            _loading.value = false
        }
    }

    private companion object {
        const val TAG = "CatalogRepository"
    }
}
