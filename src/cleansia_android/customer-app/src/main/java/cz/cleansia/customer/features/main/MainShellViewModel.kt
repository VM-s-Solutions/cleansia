package cz.cleansia.customer.features.main

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.referral.ReferralRepository
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.core.user.UserRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.launch

/**
 * Holder VM for [MainShell]. The shell warms six singleton caches on first
 * composition (addresses, catalog, orders, loyalty, referral) and reads two
 * more (user, app-settings) for the onboarding gate; exposing them via Hilt
 * keeps the shell out of the EntryPointAccessors pattern.
 *
 * No state lives here — every cache is its own singleton, the onboarding
 * gate keeps its `onboardingChecked` flag in `remember`. Pure injection seam.
 *
 * [refreshCatalog] warms the booking catalog and surfaces the snackbar on
 * failure (the repo no longer does). Connectivity failures stay silent —
 * NetworkErrorInterceptor owns the infra toast.
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
    private val snackbar: SnackbarController,
) : ViewModel() {

    fun refreshCatalog() {
        viewModelScope.launch {
            catalogRepository.refresh().onError { error ->
                if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
            }
        }
    }

    fun refreshAddresses() {
        viewModelScope.launch {
            addressRepository.refreshFromServer().onError { error ->
                if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
            }
        }
    }
}
