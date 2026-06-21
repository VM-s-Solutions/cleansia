package cz.cleansia.partner.features.profile

import androidx.lifecycle.ViewModel
import cz.cleansia.core.location.LocationService
import cz.cleansia.core.location.ReverseGeocodingService
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

/**
 * Holder VM for `AddressPickerScreen`. The picker has no persisted
 * state of its own — its resolved address lives inside the composable
 * via `remember { mutableStateOf(...) }`. This VM is purely an
 * injection seam for the three singleton services the picker
 * coordinates: FusedLocation, Mapbox geocoder, and the app-wide
 * snackbar.
 *
 * Mirrors the shape of the customer-app's `AddressManagerViewModel`
 * (also a thin Hilt seam, no state).
 */
@HiltViewModel
class AddressPickerViewModel @Inject constructor(
    val locationService: LocationService,
    val reverseGeocodingService: ReverseGeocodingService,
    val snackbar: SnackbarController,
) : ViewModel()
