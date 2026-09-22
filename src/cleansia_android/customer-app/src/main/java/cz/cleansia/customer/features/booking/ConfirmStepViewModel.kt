package cz.cleansia.customer.features.booking

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.market.InsuranceCoverage
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.core.market.insuranceCoverage
import cz.cleansia.customer.core.memberships.MembershipRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.stateIn

/**
 * Holder VM for the booking flow's confirm step. Exposes the catalog +
 * membership singleton repos via Hilt so the leaf composables can observe
 * their flows without reaching into the Application via EntryPointAccessors.
 *
 * The one derived value is the trust badge's insurance ceiling.
 */
@HiltViewModel
class ConfirmStepViewModel @Inject constructor(
    val catalogRepository: CatalogRepository,
    val membershipRepository: MembershipRepository,
    marketRepository: MarketRepository,
) : ViewModel() {

    /**
     * The insurance ceiling is per country (ADR-0060 D2) and the address wins inside a booking
     * (ADR-0058 D4): the booking's country's figure when that country is a listed market, else the
     * chosen market's. Null renders the no-figure copy — a ceiling in a guessed unit is never shown.
     */
    val insuranceCoverage: StateFlow<InsuranceCoverage?> =
        combine(catalogRepository.countryId, marketRepository.state) { bookingCountryId, market ->
            val resolved = market as? MarketState.Resolved ?: return@combine null
            val country = resolved.markets.firstOrNull { it.countryId == bookingCountryId } ?: resolved.selected
            country.insuranceCoverage
        }.stateIn(viewModelScope, SharingStarted.Eagerly, null)
}
