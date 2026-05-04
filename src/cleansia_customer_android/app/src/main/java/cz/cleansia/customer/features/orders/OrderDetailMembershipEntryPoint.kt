package cz.cleansia.customer.features.orders

import cz.cleansia.customer.core.memberships.MembershipRepository
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/**
 * Lets OrderDetailScreen reach the singleton MembershipRepository without
 * routing through OrderDetailViewModel — needed only for the "Make this
 * recurring" CTA visibility gate (PA14 Path B). Same pattern used by other
 * screens that read membership state inline (PreferredCleanerPicker, etc.).
 */
@EntryPoint
@InstallIn(SingletonComponent::class)
interface OrderDetailMembershipEntryPoint {
    fun membershipRepository(): MembershipRepository
}
