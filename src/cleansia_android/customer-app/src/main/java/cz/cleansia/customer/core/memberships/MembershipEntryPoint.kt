package cz.cleansia.customer.core.memberships

import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/**
 * Entry point for resolving [MembershipRepository] from non-Hilt Compose
 * contexts (e.g. the favorite-cleaner picker on the booking sheet, which
 * can't take it via a hiltViewModel because it's a leaf composable, not a
 * screen). Mirrors [cz.cleansia.customer.core.orders.OrderRepositoryEntryPoint].
 */
@EntryPoint
@InstallIn(SingletonComponent::class)
interface MembershipEntryPoint {
    fun membershipRepository(): MembershipRepository
}
