package cz.cleansia.customer.features.booking

import androidx.lifecycle.ViewModel
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.orders.OrderRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

/**
 * Holder VM for [PreferredCleanerPicker] — exposes the membership and order
 * repos for the Plus-only "request the same cleaner again" picker. No state
 * lives here; the screen owns its dialog/loaded flags. Replaces the previous
 * EntryPointAccessors lookup so the leaf composable goes through Hilt.
 */
@HiltViewModel
class PreferredCleanerViewModel @Inject constructor(
    val membershipRepository: MembershipRepository,
    val orderRepository: OrderRepository,
) : ViewModel()
