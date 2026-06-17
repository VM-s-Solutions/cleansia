package cz.cleansia.customer.features.booking

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.catalog.CatalogRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.launch

/**
 * Holder VM for [ServicesStep] — exposes the singleton [CatalogRepository] so
 * the step composable can observe services/packages/loading/loaded flows
 * without reaching into the Application via EntryPointAccessors. Catalog
 * cache state lives in the repo itself.
 *
 * [refreshCatalog] is the retry hook: it warms the catalog and surfaces the
 * snackbar on failure (the repo no longer does). Connectivity failures stay
 * silent — NetworkErrorInterceptor owns the infra toast.
 */
@HiltViewModel
class ServicesStepViewModel @Inject constructor(
    val catalogRepository: CatalogRepository,
    private val snackbar: SnackbarController,
) : ViewModel() {

    fun refreshCatalog() {
        viewModelScope.launch {
            catalogRepository.refresh().onError { error ->
                if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
            }
        }
    }
}
