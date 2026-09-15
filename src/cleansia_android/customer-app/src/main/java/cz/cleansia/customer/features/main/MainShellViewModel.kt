package cz.cleansia.customer.features.main

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.countryId
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.referral.ReferralRepository
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.core.user.UserRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.launch

/**
 * Injection seam for the shell's cache warming and the onboarding gate.
 *
 * **No state lives here** — every cache is its own singleton.
 */
@HiltViewModel
class MainShellViewModel @Inject constructor(
    val userRepository: UserRepository,
    val appSettings: AppSettingsRepository,
    val addressRepository: AddressRepository,
    val catalogRepository: CatalogRepository,
    val orderRepository: OrderRepository,
    val loyaltyRepository: LoyaltyRepository,
    val referralRepository: ReferralRepository,
    val marketRepository: MarketRepository,
    private val snackbar: SnackbarController,
) : ViewModel() {

    /** The catalogue is warmed for the chosen market, so the directory is read first (ADR-0058 D5). */
    fun refreshCatalog() {
        viewModelScope.launch {
            val market = marketRepository.ensureLoaded()
            catalogRepository.refresh(market.countryId).onError { error ->
                if (error !is ApiError.Network) snackbar.showError(error)
            }
        }
    }

    fun refreshAddresses() {
        viewModelScope.launch {
            addressRepository.refreshFromServer().onError { error ->
                if (error !is ApiError.Network) snackbar.showError(error)
            }
        }
    }
}
