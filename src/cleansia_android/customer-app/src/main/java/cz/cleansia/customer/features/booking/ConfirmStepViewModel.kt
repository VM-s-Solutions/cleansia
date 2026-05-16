package cz.cleansia.customer.features.booking

import androidx.lifecycle.ViewModel
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.memberships.MembershipRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

/**
 * Holder VM for the booking flow's confirm step. Exposes the catalog +
 * membership singleton repos via Hilt so the leaf composables can observe
 * their flows without reaching into the Application via EntryPointAccessors.
 *
 * No state lives here — both repos already cache their own state.
 */
@HiltViewModel
class ConfirmStepViewModel @Inject constructor(
    val catalogRepository: CatalogRepository,
    val membershipRepository: MembershipRepository,
) : ViewModel()
