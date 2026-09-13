package cz.cleansia.customer.features.auth

import androidx.lifecycle.ViewModel
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.countryId
import cz.cleansia.customer.core.referral.ReferralRepository
import cz.cleansia.customer.core.referral.ValidateReferralResponse
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

/**
 * Holder VM for [SignUpScreen]: validates a referral code during sign-up, in the market the visitor
 * chose, without the screen reaching into the Application via EntryPointAccessors.
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

    suspend fun validateReferral(code: String): ApiResult<ValidateReferralResponse> =
        referralRepository.validate(code, marketRepository.ensureLoaded().countryId)
}
