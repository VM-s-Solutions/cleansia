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
import cz.cleansia.customer.core.referral.ReferralAccountDto
import cz.cleansia.customer.core.referral.ReferralRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
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
    private val snackbar: SnackbarController,
) : ViewModel() {

    val account: StateFlow<LoyaltyAccountDto?> = loyaltyRepository.account
    val tiers: StateFlow<List<TierInfoDto>> = loyaltyRepository.tiers
    val loading: StateFlow<Boolean> = loyaltyRepository.loading
    val loaded: StateFlow<Boolean> = loyaltyRepository.loaded
    val referralAccount: StateFlow<ReferralAccountDto?> = referralRepository.account

    /** The tier floor is a platform-default-currency number, so it is labelled with the catalogue's default code. */
    val currencyCode: StateFlow<String?> = catalogRepository.currencyCode

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
