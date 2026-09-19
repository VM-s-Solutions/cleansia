package cz.cleansia.customer.features.rewards

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.loyalty.LoyaltyAccountDto
import cz.cleansia.customer.core.loyalty.LoyaltyActivityItemDto
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.loyalty.TierInfoDto
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.core.market.defaultCurrencyCode
import cz.cleansia.customer.core.referral.ReferralAccountDto
import cz.cleansia.customer.core.referral.ReferralRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

/**
 * Injection seam for the rewards tab's three singleton repositories. Almost no state lives here:
 * the activity preview is the one exception, because the repository does not cache it.
 */
@HiltViewModel
class RewardsTabViewModel @Inject constructor(
    private val loyaltyRepository: LoyaltyRepository,
    private val referralRepository: ReferralRepository,
    catalogRepository: CatalogRepository,
    marketRepository: MarketRepository,
    private val snackbar: SnackbarController,
) : ViewModel() {

    val account: StateFlow<LoyaltyAccountDto?> = loyaltyRepository.account
    val tiers: StateFlow<List<TierInfoDto>> = loyaltyRepository.tiers
    val loading: StateFlow<Boolean> = loyaltyRepository.loading
    val loaded: StateFlow<Boolean> = loyaltyRepository.loaded
    val referralAccount: StateFlow<ReferralAccountDto?> = referralRepository.account

    /** The market's currency when one resolved, else the catalogue default the server prices in. */
    val currencyCode: StateFlow<String?> =
        combine(marketRepository.state, catalogRepository.currencyCode) { market, catalogDefault ->
            (market as? MarketState.Resolved)?.selected?.currencyCode ?: catalogDefault
        }.stateIn(viewModelScope, SharingStarted.Eagerly, null)

    /**
     * The tier floor is a platform-default-currency number enforced only on orders in that currency
     * (ADR-0058 D5), so the floor line renders only when the market's currency is the default —
     * otherwise no floor applies and nothing renders a guessed unit. With no market resolved the
     * server prices the default, and the floor applies.
     */
    val tierFloorApplies: StateFlow<Boolean> = marketRepository.state
        .map { market ->
            when (market) {
                MarketState.Unavailable -> true
                is MarketState.Resolved -> market.selected.currencyCode == market.defaultCurrencyCode
            }
        }
        .stateIn(viewModelScope, SharingStarted.Eagerly, true)

    private val _activityPreview = MutableStateFlow<List<LoyaltyActivityItemDto>>(emptyList())
    val activityPreview: StateFlow<List<LoyaltyActivityItemDto>> = _activityPreview.asStateFlow()

    fun loadActivityPreview() {
        viewModelScope.launch { fetchActivityPreview() }
    }

    fun refresh() {
        viewModelScope.launch {
            loyaltyRepository.refresh().showErrorUnlessNetwork()
            referralRepository.refresh().showErrorUnlessNetwork()
            fetchActivityPreview()
        }
    }

    fun onReferralCodeCopied() {
        snackbar.showSuccessKey(R.string.loyalty_referral_copied_toast)
    }

    fun onReferralShareUnavailable() {
        snackbar.showInfoKey(R.string.loyalty_referral_share_failed)
    }

    private suspend fun fetchActivityPreview() {
        loyaltyRepository.loadActivity(offset = 0, limit = PREVIEW_LIMIT)
            .onSuccess { _activityPreview.value = it.data }
            .showErrorUnlessNetwork()
    }

    private fun <T> ApiResult<T>.showErrorUnlessNetwork(): ApiResult<T> = onError { error ->
        if (error !is ApiError.Network) snackbar.showError(error)
    }

    private companion object {
        const val PREVIEW_LIMIT = 5
    }
}
