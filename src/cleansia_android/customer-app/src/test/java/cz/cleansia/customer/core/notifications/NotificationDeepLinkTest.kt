package cz.cleansia.customer.core.notifications

import cz.cleansia.customer.features.main.MainTab
import cz.cleansia.customer.navigation.Routes
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * Where a tapped notification lands.
 *
 * The membership cases are why this file exists. Both `membership.expiring_soon` and
 * `membership.cancellation_effective` are addressed to somebody who ALREADY has a subscription — it is
 * about to lapse, or a cancellation has just taken effect. They used to route to
 * `Routes.SubscribePlus`, the sales page, which answered a question the recipient had not asked and
 * buried the one they had. They now land on the Profile tab, where `MembershipManagementCard` renders
 * both states: manage-and-cancel for a live subscription, subscribe for a lapsed one.
 *
 * The pure `resolve(eventKey, args)` overload is the one under test because it is the one both
 * surfaces share — the push tap path and the notifications-inbox feed. Testing it here means the two
 * cannot drift onto different destinations for the same event.
 */
class NotificationDeepLinkTest {

    @Test
    fun `membership events land on the profile tab, not the sales page`() {
        for (key in listOf("membership.expiring_soon", "membership.cancellation_effective")) {
            assertEquals(
                "$key must open the membership management surface",
                Routes.Home(tab = MainTab.Profile.name),
                NotificationDeepLink.resolve(key, emptyMap()),
            )
        }
    }

    /**
     * Anti-vacuity: the assertion above passes on a resolver that returns the same thing for
     * everything, which is exactly how a routing table rots. These pin that other events still go
     * somewhere ELSE, and that the ordinary Home navigation is untouched by the new tab parameter.
     */
    @Test
    fun `other events still resolve to their own destinations`() {
        assertEquals(
            Routes.OrderDetail("ord-1"),
            NotificationDeepLink.resolve("order.completed", mapOf("orderId" to "ord-1")),
        )
        assertEquals(
            Routes.DisputeDetail("dis-1"),
            NotificationDeepLink.resolve("dispute.reply", mapOf("disputeId" to "dis-1")),
        )
        assertEquals(
            Routes.RewardsActivity,
            NotificationDeepLink.resolve("loyalty.tier_upgrade", emptyMap()),
        )
    }

    /** A default Home carries no tab, so every ordinary navigation still opens on Home. */
    @Test
    fun `home defaults to no tab`() {
        assertNull(Routes.Home().tab)
        assertEquals(Routes.Home(), Routes.Home(tab = null))
    }

    @Test
    fun `an order event without an order id resolves to nothing`() {
        assertNull(NotificationDeepLink.resolve("order.completed", emptyMap()))
        assertNull(NotificationDeepLink.resolve("order.completed", mapOf("orderId" to "  ")))
    }

    @Test
    fun `an unknown event resolves to nothing rather than guessing`() {
        assertNull(NotificationDeepLink.resolve("promo.new_sitewide", emptyMap()))
        assertNull(NotificationDeepLink.resolve("not.a.real.event", emptyMap()))
    }
}
