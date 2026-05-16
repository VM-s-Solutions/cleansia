package cz.cleansia.customer.core.loyalty

import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/**
 * Hilt EntryPoint for non-VM composables that need direct access to the
 * loyalty repo (e.g. MainShell prefetch, Rewards tab observers).
 */
@EntryPoint
@InstallIn(SingletonComponent::class)
interface LoyaltyRepositoryEntryPoint {
    fun loyaltyRepository(): LoyaltyRepository
}
