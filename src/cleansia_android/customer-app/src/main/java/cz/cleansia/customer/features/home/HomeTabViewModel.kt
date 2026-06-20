package cz.cleansia.customer.features.home

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.launch

/**
 * Holder VM for [HomeTab]. The home screen observes six singleton repositories
 * (address, orders, loyalty, membership, catalog, recurring); exposing them
 * via Hilt-injected fields keeps the screen out of the EntryPointAccessors
 * pattern. State already lives in the singletons — this VM is purely an
 * injection seam, no per-VM state.
 *
 * [refreshCatalog] warms the catalog for the popular-packages strip and
 * surfaces the snackbar on failure (the repo no longer does). Connectivity
 * failures stay silent — NetworkErrorInterceptor owns the infra toast.
 */
@HiltViewModel
class HomeTabViewModel @Inject constructor(
    val addressRepository: AddressRepository,
    val orderRepository: OrderRepository,
    val loyaltyRepository: LoyaltyRepository,
    val membershipRepository: MembershipRepository,
    val catalogRepository: CatalogRepository,
    val recurringBookingRepository: RecurringBookingRepository,
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
