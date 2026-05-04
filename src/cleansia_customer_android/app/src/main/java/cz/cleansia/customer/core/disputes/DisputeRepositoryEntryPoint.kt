package cz.cleansia.customer.core.disputes

import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/**
 * Entry point for manually resolving [DisputeRepository] from non-Hilt
 * contexts (e.g. OkHttp interceptors or Authenticators constructed via
 * factory). Mirrors the pattern used by [cz.cleansia.customer.core.orders.OrderRepositoryEntryPoint].
 */
@EntryPoint
@InstallIn(SingletonComponent::class)
interface DisputeRepositoryEntryPoint {
    fun disputeRepository(): DisputeRepository
}
