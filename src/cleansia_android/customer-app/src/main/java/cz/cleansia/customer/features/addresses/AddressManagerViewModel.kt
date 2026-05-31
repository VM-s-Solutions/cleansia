package cz.cleansia.customer.features.addresses

import androidx.lifecycle.ViewModel
import cz.cleansia.core.location.LocationService
import cz.cleansia.core.location.ReverseGeocodingService
import cz.cleansia.core.servicearea.ServiceAreaProvider
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.data.AddressRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

/**
 * Holder VM for [AddressManagerScreen]. The screen orchestrates four singleton
 * services (saved-address cache, FusedLocation, reverse geocoder, snackbar);
 * exposing them via Hilt-injected fields keeps the screen out of the
 * EntryPointAccessors / hand-rolled Application-reach pattern.
 *
 * No state lives here on purpose — all the address-state already lives in the
 * singleton [AddressRepository], and the screen owns its own pane/picker
 * state with `remember { mutableStateOf(...) }`. This VM is purely an
 * injection seam.
 */
@HiltViewModel
class AddressManagerViewModel @Inject constructor(
    val addressRepository: AddressRepository,
    val locationService: LocationService,
    val reverseGeocodingService: ReverseGeocodingService,
    val serviceAreaProvider: ServiceAreaProvider,
    val snackbar: SnackbarController,
) : ViewModel()
