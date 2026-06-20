package cz.cleansia.customer.core.orders
import cz.cleansia.core.auth.TokenStore

import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/**
 * Entry point for manually resolving [OrderRepository] from non-Hilt contexts
 * (e.g. OkHttp interceptors or Authenticators constructed via factory).
 * Mirrors the pattern used by `AddressRepository` / `TokenStore`.
 *
 * Also exposes the singleton [SnackbarController] so the still-non-VM
 * [cz.cleansia.customer.features.orders.OrdersTab] composable can surface a
 * repository failure itself (the repo no longer toasts).
 */
@EntryPoint
@InstallIn(SingletonComponent::class)
interface OrderRepositoryEntryPoint {
    fun orderRepository(): OrderRepository
    fun snackbarController(): SnackbarController
}
