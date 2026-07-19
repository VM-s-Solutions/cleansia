package cz.cleansia.partner.core.notifications

import android.content.Context
import cz.cleansia.partner.R
import cz.cleansia.partner.navigation.NavRoute
import io.mockk.every
import io.mockk.mockk
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * Guards the render + deep-link behavior the feed migration reuses: push and
 * feed resolve one template map, and both row-tap and system-tap resolve one
 * [NotificationDeepLink]. Unknown keys drop; the digest key lands on Main.
 */
class NotificationTemplatesTest {

    @Test
    fun `templateFor maps the new-jobs digest to the new-jobs channel`() {
        val template = NotificationTemplates.templateFor("order.new_available")

        assertEquals(R.string.notification_new_jobs_title, template?.titleRes)
        assertEquals(R.string.notification_new_jobs_body, template?.bodyRes)
        assertEquals(NotificationChannels.CHANNEL_NEW_JOBS, template?.channelId)
    }

    @Test
    fun `templateFor maps an order-lifecycle key to the order-updates channel`() {
        val template = NotificationTemplates.templateFor("order.completed")

        assertEquals(NotificationChannels.CHANNEL_ORDER_UPDATES, template?.channelId)
    }

    @Test
    fun `templateFor returns null for an unknown key - drop parity`() {
        assertNull(NotificationTemplates.templateFor("promo.new_sitewide"))
        assertNull(NotificationTemplates.templateFor("loyalty.tier_upgrade"))
    }

    @Test
    fun `formatBody substitutes the orderNumber for order events`() {
        val context = mockk<Context>()
        every {
            context.getString(R.string.notification_order_completed_body, "A-1042")
        } returns "Job #A-1042 is complete."

        val body = NotificationTemplates.formatBody(
            context,
            "order.completed",
            R.string.notification_order_completed_body,
            mapOf("orderNumber" to "A-1042"),
        )

        assertEquals("Job #A-1042 is complete.", body)
    }

    @Test
    fun `formatBody substitutes the count for the new-jobs digest`() {
        val context = mockk<Context>()
        every {
            context.getString(R.string.notification_new_jobs_body, 4)
        } returns "4 new jobs available near you."

        val body = NotificationTemplates.formatBody(
            context,
            "order.new_available",
            R.string.notification_new_jobs_body,
            mapOf("count" to "4"),
        )

        assertEquals("4 new jobs available near you.", body)
    }

    @Test
    fun `formatBody falls back to a count of one when the digest count is missing`() {
        val context = mockk<Context>()
        every {
            context.getString(R.string.notification_new_jobs_body, 1)
        } returns "1 new job available near you."

        val body = NotificationTemplates.formatBody(
            context,
            "order.new_available",
            R.string.notification_new_jobs_body,
            emptyMap(),
        )

        assertEquals("1 new job available near you.", body)
    }

    @Test
    fun `deep link resolves the new-jobs digest to Main`() {
        assertEquals(NavRoute.Main, NotificationDeepLink.resolve("order.new_available", null))
    }

    @Test
    fun `deep link resolves an order event to the order detail`() {
        assertEquals(
            NavRoute.OrderDetail(orderId = "ord-7"),
            NotificationDeepLink.resolve("order.confirmed", "ord-7"),
        )
    }

    @Test
    fun `deep link resolves an unknown key to null`() {
        assertNull(NotificationDeepLink.resolve("promo.new_sitewide", null))
    }
}
