package cz.cleansia.customer.features.home

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.notifications.NotificationFeedRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.launch

/**
 * Injection seam for the home screen's six singleton repositories.
 *
 * **No state lives here** — it exists so the screen avoids the EntryPointAccessors pattern.
 */
@HiltViewModel
class HomeTabViewModel @Inject constructor(
    val addressRepository: AddressRepository,
    val orderRepository: OrderRepository,
    val loyaltyRepository: LoyaltyRepository,
    val membershipRepository: MembershipRepository,
    val catalogRepository: CatalogRepository,
    val recurringBookingRepository: RecurringBookingRepository,
    val notificationFeedRepository: NotificationFeedRepository,
    private val snackbar: SnackbarController,
) : ViewModel() {

    fun refreshCatalog() {
        viewModelScope.launch {
            catalogRepository.refresh().onError { error ->
                if (error !is ApiError.Network) snackbar.showError(error)
            }
        }
    }

    /**
     * Refetch the bell's unread count. Silent on failure — the badge is
     * ambient chrome; a stale (or absent) count self-heals on the next
     * foreground/Home entry and on every inbox open.
     */
    fun refreshNotificationBadge() {
        viewModelScope.launch { notificationFeedRepository.refreshUnreadCount() }
    }

    /**
     * Home entry — a tab switch back, or the process returning to foreground.
     *
     * **The observer is re-attached to an already-STARTED lifecycle on every recomposition, so this runs
     * far more often than once per foreground.** That is why each source is gated on its own freshness
     * watermark: ungated, every tap on Home would cost three network calls.
     * -> /mobile-app/patterns#session-wipe
     */
    fun onResume() {
        refreshIfStale(loyaltyRepository.staleness) { loyaltyRepository.refresh() }
        refreshIfStale(orderRepository.staleness) { orderRepository.refresh() }
        refreshIfStale(membershipRepository.staleness) { membershipRepository.refresh() }
    }

    /** Each source gets its own coroutine so a slow one cannot hold up the others. */
    private fun refreshIfStale(staleness: Staleness, refresh: suspend () -> Unit) {
        if (!staleness.isStale()) return
        viewModelScope.launch { refresh() }
    }
}
