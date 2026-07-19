package cz.cleansia.customer.core.auth
import cz.cleansia.core.auth.AuthAuthenticator

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.disputes.DisputeRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.customer.core.notifications.NotificationFeedRepository
import cz.cleansia.customer.core.notifications.NotificationPreferencesRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.core.referral.ReferralRepository
import cz.cleansia.customer.core.user.UserRepository
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet

/**
 * Aggregates every [SessionScopedCache] implementor into a Hilt multibinding.
 *
 * [AuthRepository.logout] and [AuthAuthenticator] both inject the resulting
 * `Set<SessionScopedCache>` and iterate it on sign-out — that replaces the
 * old hand-maintained list of seven `Provider<T>` constructor params per side
 * (which previously had to stay in sync by convention).
 *
 * To register a new cache:
 *   1. Have the repo implement [SessionScopedCache] (one `override suspend fun clear()`).
 *   2. Add a `@Binds @IntoSet` line below.
 *   That's it — both clear-paths pick it up automatically.
 */
@Module
@InstallIn(SingletonComponent::class)
abstract class SessionScopedModule {

    @Binds @IntoSet
    abstract fun bindAddressRepository(impl: AddressRepository): SessionScopedCache

    @Binds @IntoSet
    abstract fun bindOrderRepository(impl: OrderRepository): SessionScopedCache

    @Binds @IntoSet
    abstract fun bindDisputeRepository(impl: DisputeRepository): SessionScopedCache

    @Binds @IntoSet
    abstract fun bindLoyaltyRepository(impl: LoyaltyRepository): SessionScopedCache

    @Binds @IntoSet
    abstract fun bindReferralRepository(impl: ReferralRepository): SessionScopedCache

    @Binds @IntoSet
    abstract fun bindMembershipRepository(impl: MembershipRepository): SessionScopedCache

    @Binds @IntoSet
    abstract fun bindRecurringBookingRepository(impl: RecurringBookingRepository): SessionScopedCache

    @Binds @IntoSet
    abstract fun bindPushTokenRepository(impl: PushTokenRepository): SessionScopedCache

    @Binds @IntoSet
    abstract fun bindUserRepository(impl: UserRepository): SessionScopedCache

    @Binds @IntoSet
    abstract fun bindNotificationPreferencesRepository(
        impl: NotificationPreferencesRepository,
    ): SessionScopedCache

    @Binds @IntoSet
    abstract fun bindNotificationFeedRepository(impl: NotificationFeedRepository): SessionScopedCache
}
