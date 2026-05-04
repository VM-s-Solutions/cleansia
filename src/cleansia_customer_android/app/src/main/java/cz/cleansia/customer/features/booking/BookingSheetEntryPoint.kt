package cz.cleansia.customer.features.booking

import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.ui.snackbar.SnackbarController
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

@EntryPoint
@InstallIn(SingletonComponent::class)
interface BookingSheetEntryPoint {
    fun addressRepository(): AddressRepository
    fun orderRepository(): OrderRepository
    fun catalogRepository(): CatalogRepository
    fun snackbarController(): SnackbarController
}
