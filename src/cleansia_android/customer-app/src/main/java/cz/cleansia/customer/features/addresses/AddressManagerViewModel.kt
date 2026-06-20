package cz.cleansia.customer.features.addresses

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.location.LocationService
import cz.cleansia.core.location.ReverseGeocodingService
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.servicearea.ServiceAreaProvider
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.data.UserAddress
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.launch

/**
 * Holder VM for [AddressManagerScreen]. The screen orchestrates four singleton
 * services (saved-address cache, FusedLocation, reverse geocoder, snackbar);
 * exposing them via Hilt-injected fields keeps the screen out of the
 * EntryPointAccessors / hand-rolled Application-reach pattern.
 *
 * Address state still lives in the singleton [AddressRepository] and the screen
 * owns its pane/picker state. The mutation wrappers below surface the snackbar
 * on failure (the repo no longer does); connectivity failures stay silent —
 * NetworkErrorInterceptor owns the infra toast.
 */
@HiltViewModel
class AddressManagerViewModel @Inject constructor(
    val addressRepository: AddressRepository,
    val locationService: LocationService,
    val reverseGeocodingService: ReverseGeocodingService,
    val serviceAreaProvider: ServiceAreaProvider,
    val snackbar: SnackbarController,
) : ViewModel() {

    /**
     * Persist [address], then select it and run [onDone]. The post-save
     * selection + navigation run regardless of the upsert outcome (the legacy
     * flow ignored the result and always navigated); a failure surfaces the
     * snackbar in addition.
     */
    fun saveAndSelect(address: UserAddress, onDone: () -> Unit) {
        viewModelScope.launch {
            addressRepository.upsert(address).onError(::surface)
            addressRepository.setSelected(address.id)
            onDone()
        }
    }

    fun setDefault(id: String) {
        viewModelScope.launch { addressRepository.setDefault(id).onError(::surface) }
    }

    fun delete(id: String) {
        viewModelScope.launch { addressRepository.delete(id).onError(::surface) }
    }

    fun rename(id: String, newLabel: String) {
        viewModelScope.launch { addressRepository.rename(id, newLabel).onError(::surface) }
    }

    private fun surface(error: ApiError) {
        if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
    }
}
