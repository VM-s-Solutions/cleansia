package cz.cleansia.customer.core.orders
import cz.cleansia.core.auth.TokenStore

import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/**
 * Entry point for manually resolving [OrderRepository] from non-Hilt contexts
 * (e.g. OkHttp interceptors or Authenticators constructed via factory).
 * Mirrors the pattern used by `AddressRepository` / `TokenStore`.
 */
@EntryPoint
@InstallIn(SingletonComponent::class)
interface OrderRepositoryEntryPoint {
    fun orderRepository(): OrderRepository
}
