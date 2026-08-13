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
 * Aggregates every session-scoped cache into a Hilt multibinding, which sign-out and the authenticator
 * both iterate.
 *
 * **This replaced a hand-maintained list of provider params per side** — the shape that let a new cache
 * be added on one side and forgotten on the other. -> /mobile-app/patterns#session-wipe
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
