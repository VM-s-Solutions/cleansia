package cz.cleansia.customer.core.referral

import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/**
 * Hilt EntryPoint for non-VM composables that need direct access to the
 * referral repo (e.g. MainShell prefetch, Rewards tab "Invite friends" card).
 */
@EntryPoint
@InstallIn(SingletonComponent::class)
interface ReferralRepositoryEntryPoint {
    fun referralRepository(): ReferralRepository
}
