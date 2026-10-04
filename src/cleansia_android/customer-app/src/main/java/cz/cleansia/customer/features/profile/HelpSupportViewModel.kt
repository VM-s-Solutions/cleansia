package cz.cleansia.customer.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
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
    private val snackbar: SnackbarController,
) : ViewModel() {

    /** The FAQ's insurance ceiling is the chosen market's (ADR-0060 D2); null leaves the insurance question out. */
    val insuranceCoverage: StateFlow<InsuranceCoverage?> = marketRepository.state
        .map { it.selectedOrNull?.insuranceCoverage }
        .stateIn(viewModelScope, SharingStarted.Eagerly, null)

    /** No mail app took the support address, so the screen copied it; say so. */
    fun onEmailUnavailable() = snackbar.showInfoKey(R.string.help_email_unavailable)

    /** No dialer took the support line, so the screen copied the number; say so. */
    fun onCallUnavailable() = snackbar.showInfoKey(R.string.help_call_unavailable)
}
