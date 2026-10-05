package cz.cleansia.customer.features.auth

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.market.MarketListItem
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.countryId
import cz.cleansia.customer.core.market.selectedOrNull
import cz.cleansia.customer.core.referral.ReferralRepository
import cz.cleansia.customer.core.referral.ValidateReferralResponse
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

/**
 * Holder VM for [SignUpScreen]: validates a referral code during sign-up, in the market the visitor
 * chose, and exposes that market for the code sheet's referral credit, without the screen reaching
 * into the Application via EntryPointAccessors.
 *
 * Validate is safe without a token — AuthInterceptor skips the Authorization
 * header when TokenStore is empty, and the backend endpoint is
 * [AllowAnonymous].
 */
@HiltViewModel
class SignUpViewModel @Inject constructor(
    private val referralRepository: ReferralRepository,
    private val marketRepository: MarketRepository,
) : ViewModel() {

    val selectedMarket: StateFlow<MarketListItem?> = marketRepository.state
        .map { it.selectedOrNull }
        .stateIn(viewModelScope, SharingStarted.Eagerly, null)

    init {
        viewModelScope.launch { marketRepository.ensureLoaded() }
    }

    suspend fun validateReferral(code: String): ApiResult<ValidateReferralResponse> =
        referralRepository.validate(code, marketRepository.ensureLoaded().countryId)
}
