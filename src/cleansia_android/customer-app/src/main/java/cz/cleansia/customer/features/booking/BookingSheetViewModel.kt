package cz.cleansia.customer.features.booking

import androidx.lifecycle.ViewModel
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

/**
 * Holder VM for the booking bottom-sheet shell. Exposes the four singletons
 * the sheet needs (saved addresses, orders cache for rebook prefill, catalog
 * lookup, snackbar) without the sheet reaching into the Application via
 * EntryPointAccessors. The sheet's own form state lives in [BookingViewModel];
 * this VM is a pure injection seam.
 */
@HiltViewModel
class BookingSheetViewModel @Inject constructor(
    val addressRepository: AddressRepository,
    val orderRepository: OrderRepository,
    val catalogRepository: CatalogRepository,
    val snackbarController: SnackbarController,
) : ViewModel()
