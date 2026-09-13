package cz.cleansia.customer.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.core.market.InsuranceCoverage
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.insuranceCoverage
import cz.cleansia.customer.core.market.selectedOrNull
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn

@HiltViewModel
class HelpSupportViewModel @Inject constructor(
    marketRepository: MarketRepository,
) : ViewModel() {

    /** The FAQ's insurance ceiling is the chosen market's (ADR-0060 D2); null renders the no-figure answer. */
    val insuranceCoverage: StateFlow<InsuranceCoverage?> = marketRepository.state
        .map { it.selectedOrNull?.insuranceCoverage }
        .stateIn(viewModelScope, SharingStarted.Eagerly, null)
}
