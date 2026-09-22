package cz.cleansia.customer.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch

@HiltViewModel
class MarketViewModel @Inject constructor(
    private val marketRepository: MarketRepository,
) : ViewModel() {

    val state: StateFlow<MarketState> = marketRepository.state

    /** Persists the choice; every reader observes the repository, so nothing else is told. */
    fun select(isoCode: String) {
        viewModelScope.launch { marketRepository.select(isoCode) }
    }
}
