package cz.cleansia.partner.core.notifications

import android.content.Context
import cz.cleansia.partner.R
import cz.cleansia.partner.navigation.NavRoute
import io.mockk.every
import io.mockk.mockk
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Guards the render + deep-link behavior the feed migration reuses: push and
 * feed resolve one template map, and both row-tap and system-tap resolve one
 * [NotificationDeepLink]. Unknown keys drop; the digest key lands on Main.
 */
class NotificationTemplatesTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /**
     * The three job reminders. Their own roster rather than an extension of [reassignKeys], because the
     * digest body takes a COUNT (`%1${'$'}d`) where every reassign body takes an order number
     * (`%1${'$'}s`) — folding them together would have meant weakening the slot assertion to accept
     * either, which is the assertion's whole value.
     */
    private val reminderKeys = listOf(
        "notification_reminder_tomorrow_title",
        "notification_reminder_tomorrow_body",
        "notification_reminder_soon_title",
        "notification_reminder_soon_body",
        "notification_reminder_not_started_title",
        "notification_reminder_not_started_body",
    )

    /**
     * The weekly-cap notice. Its own roster for the same reason as [reminderKeys]: the body takes the
     * CAP as a count (`%1${'$'}d`) where every reassign body takes an order number (`%1${'$'}s`).
     */
    private val weeklyLimitKeys = listOf(
        "notification_employee_weekly_limit_set_title",
        "notification_employee_weekly_limit_set_body",
    )

    private val reassignKeys = listOf(
        "notification_order_assigned_title",
        "notification_order_assigned_body",
        "notification_order_assignment_revoked_title",
        "notification_order_assignment_revoked_body",
    )

    /**
     * The word each locale uses for a cancelled job. The revocation must not reach for any of them:
     * the job goes ahead with somebody else, and a cleaner repeating our wording to the customer
     * would be telling them their booking was cancelled.
     */
    private val cancelledWord = mapOf(
        "values" to "cancel",
        "values-cs" to "zrušen",
        "values-sk" to "zrušen",
        "values-uk" to "скасован",
        "values-ru" to "отмен",
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

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
    fun `templateFor maps the payroll payout to the order-updates channel`() {
        val template = NotificationTemplates.templateFor("payroll.invoice_paid")

        assertEquals(R.string.notification_payroll_invoice_paid_title, template?.titleRes)
        assertEquals(R.string.notification_payroll_invoice_paid_body, template?.bodyRes)
        assertEquals(NotificationChannels.CHANNEL_ORDER_UPDATES, template?.channelId)
    }

    /**
     * The targeted offer rides the new-jobs channel because the server treats it as
     * `NotificationCategory.NewJobsAvailable` (the resolver declines outright for a cleaner who
     * muted new jobs). A channel of its own would be a system-level toggle that mute cannot reach.
     */
    @Test
    fun `templateFor maps the preferred offer to the new-jobs channel`() {
        val template = NotificationTemplates.templateFor("order.preferred_offer")

        assertEquals(R.string.notification_preferred_offer_title, template?.titleRes)
        assertEquals(R.string.notification_preferred_offer_body, template?.bodyRes)
        assertEquals(NotificationChannels.CHANNEL_NEW_JOBS, template?.channelId)
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
    fun `formatBody substitutes the orderNumber for the preferred offer`() {
        val context = mockk<Context>()
        every {
            context.getString(R.string.notification_preferred_offer_body, "A-1042")
        } returns "Order A-1042 — someone you've cleaned for before requested you."

        val body = NotificationTemplates.formatBody(
            context,
            "order.preferred_offer",
            R.string.notification_preferred_offer_body,
            mapOf("orderId" to "ord-7", "orderNumber" to "A-1042"),
        )

        assertEquals("Order A-1042 — someone you've cleaned for before requested you.", body)
    }

    @Test
    fun `deep link resolves the preferred offer to the order detail`() {
        assertEquals(
            NavRoute.OrderDetail(orderId = "ord-7"),
            NotificationDeepLink.resolve("order.preferred_offer", "ord-7", null),
        )
    }

    @Test
    fun `deep link resolves the new-jobs digest to Main`() {
        assertEquals(NavRoute.Main, NotificationDeepLink.resolve("order.new_available", null, null))
    }

    @Test
    fun `deep link resolves an order event to the order detail`() {
        assertEquals(
            NavRoute.OrderDetail(orderId = "ord-7"),
            NotificationDeepLink.resolve("order.confirmed", "ord-7", null),
        )
    }

    @Test
    fun `deep link resolves the payroll payout to the invoice detail`() {
        assertEquals(
            NavRoute.InvoiceDetail(invoiceId = "inv-42"),
            NotificationDeepLink.resolve("payroll.invoice_paid", null, "inv-42"),
        )
    }

    @Test
    fun `deep link resolves the payroll payout to Earnings when the invoice id is absent`() {
        assertEquals(NavRoute.Earnings, NotificationDeepLink.resolve("payroll.invoice_paid", null, null))
    }

    @Test
    fun `deep link resolves the payroll payout to Earnings when the invoice id is blank`() {
        assertEquals(NavRoute.Earnings, NotificationDeepLink.resolve("payroll.invoice_paid", null, "  "))
    }

    @Test
    fun `deep link resolves an unknown key to null`() {
        assertNull(NotificationDeepLink.resolve("promo.new_sitewide", null, null))
    }

    /**
     * Both admin-reassign events ride the order-updates channel, not new-jobs: the server returns a
     * null category for them precisely so they cannot be muted, and new-jobs is the one channel a
     * cleaner silences to stop hearing about work they have not accepted.
     */
    @Test
    fun `templateFor maps the admin assignment to the order-updates channel`() {
        val template = NotificationTemplates.templateFor("order.assigned")

        assertEquals(R.string.notification_order_assigned_title, template?.titleRes)
        assertEquals(R.string.notification_order_assigned_body, template?.bodyRes)
        assertEquals(NotificationChannels.CHANNEL_ORDER_UPDATES, template?.channelId)
    }

    @Test
    fun `templateFor maps the assignment revocation to the order-updates channel`() {
        val template = NotificationTemplates.templateFor("order.assignment_revoked")

        assertEquals(R.string.notification_order_assignment_revoked_title, template?.titleRes)
        assertEquals(R.string.notification_order_assignment_revoked_body, template?.bodyRes)
        assertEquals(NotificationChannels.CHANNEL_ORDER_UPDATES, template?.channelId)
    }

    @Test
    fun `formatBody substitutes the orderNumber into the admin assignment body`() {
        val context = mockk<Context>()
        every {
            context.getString(R.string.notification_order_assigned_body, "A-1042")
        } returns "You've been assigned job #A-1042."

        val body = NotificationTemplates.formatBody(
            context,
            "order.assigned",
            R.string.notification_order_assigned_body,
            mapOf("orderId" to "ord-7", "orderNumber" to "A-1042"),
        )

        assertEquals("You've been assigned job #A-1042.", body)
    }

    @Test
    fun `formatBody substitutes the orderNumber into the revocation body`() {
        val context = mockk<Context>()
        every {
            context.getString(R.string.notification_order_assignment_revoked_body, "A-1042")
        } returns "Job #A-1042 has been reassigned."

        val body = NotificationTemplates.formatBody(
            context,
            "order.assignment_revoked",
            R.string.notification_order_assignment_revoked_body,
            mapOf("orderId" to "ord-7", "orderNumber" to "A-1042"),
        )

        assertEquals("Job #A-1042 has been reassigned.", body)
    }

    @Test
    fun `deep link resolves the admin assignment to the order detail`() {
        assertEquals(
            NavRoute.OrderDetail(orderId = "ord-7"),
            NotificationDeepLink.resolve("order.assigned", "ord-7", null),
        )
    }

    @Test
    fun `deep link resolves the revocation to the order detail`() {
        assertEquals(
            NavRoute.OrderDetail(orderId = "ord-7"),
            NotificationDeepLink.resolve("order.assignment_revoked", "ord-7", null),
        )
    }

    @Test
    fun `the admin-reassign copy is translated in all five locales`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            reassignKeys.forEach { key ->
                val value = valueOf(xml, key)
                assertNotNull("$locale/strings.xml is missing $key", value)
                assertTrue("$locale/strings.xml has a blank $key", value!!.isNotBlank())
            }
        }
    }

    @Test
    fun `the four translations of the admin-reassign copy are not the English string copied over`() {
        val english = reassignKeys.associateWith { valueOf(stringsXml("values"), it) }
        locales.drop(1).forEach { locale ->
            val xml = stringsXml(locale)
            reassignKeys.forEach { key ->
                assertTrue(
                    "$locale/strings.xml left $key in English",
                    valueOf(xml, key) != english[key],
                )
            }
        }
    }

    @Test
    fun `the job-reminder copy is translated in all five locales`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            reminderKeys.forEach { key ->
                val value = valueOf(xml, key)
                assertNotNull("$locale/strings.xml is missing $key", value)
                assertTrue("$locale/strings.xml has a blank $key", value!!.isNotBlank())
            }
        }
    }

    @Test
    fun `the four translations of the job-reminder copy are not the English string copied over`() {
        val english = reminderKeys.associateWith { valueOf(stringsXml("values"), it) }
        locales.drop(1).forEach { locale ->
            val xml = stringsXml(locale)
            reminderKeys.forEach { key ->
                assertTrue(
                    "$locale/strings.xml left $key in English",
                    valueOf(xml, key) != english[key],
                )
            }
        }
    }

    /**
     * The digest body takes an integer count and the two per-job bodies take an order number. A bare
     * `%s` beside a positional one crashes getString at runtime, and a `%s` where the code passes an
     * Int is a format exception on the cleaner's lock screen — neither shows up until it ships.
     */
    @Test
    fun `each job-reminder body takes its own argument and each title takes nothing`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            reminderKeys.forEach { key ->
                val expected = when {
                    key == "notification_reminder_tomorrow_body" -> listOf("%1${'$'}d")
                    key.endsWith("_body") -> listOf("%1${'$'}s")
                    else -> emptyList()
                }
                assertEquals(
                    "$locale/strings.xml passes the wrong format arguments to $key",
                    expected,
                    formatSlots(valueOf(xml, key)!!),
                )
            }
        }
    }

    @Test
    fun `each admin-reassign body takes the order number and each title takes nothing`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            reassignKeys.forEach { key ->
                val expected = if (key.endsWith("_body")) listOf("%1\$s") else emptyList()
                assertEquals(
                    "$locale/strings.xml passes the wrong format arguments to $key",
                    expected,
                    formatSlots(valueOf(xml, key)!!),
                )
            }
        }
    }

    /** The job goes ahead with someone else — saying "cancelled" hands the cleaner the wrong sentence. */
    @Test
    fun `no revocation string calls the job cancelled in any locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            reassignKeys.filter { it.contains("revoked") }.forEach { key ->
                val value = valueOf(xml, key)!!
                assertFalse(
                    "$locale/strings.xml calls the reassigned job cancelled in $key: \"$value\"",
                    value.contains(cancelledWord.getValue(locale), ignoreCase = true),
                )
            }
        }
    }

    /**
     * The badge counts feed rows. A key with a template but no row inflates it, which is the hazard
     * `NotificationFeedEventKeys`' own doc-comment states, reached from this side.
     */
    @Test
    fun `every feed key renders and the badge bumps off the keyset`() {
        PartnerFeedEventKeys.all.forEach { key ->
            assertNotNull(
                "feed key $key has no template — its row would be dropped unrendered",
                NotificationTemplates.templateFor(key),
            )
        }

        val service = File(
            resDir.parentFile,
            "java/cz/cleansia/partner/core/notifications/CleansiaFirebaseMessagingService.kt",
        )
        assertTrue("CleansiaFirebaseMessagingService.kt not found next to res/", service.isFile)
        assertTrue(
            "the badge bump is no longer gated on the feed keyset",
            service.readText().contains("if (PartnerFeedEventKeys.contains(eventKey))"),
        )
    }

    private fun stringsXml(locale: String): String {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    private fun valueOf(xml: String, key: String): String? =
        Regex("<string name=\"$key\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(xml)
            ?.groupValues
            ?.get(1)

    /** Positional and bare alike — a bare `%s` beside a positional one crashes `getString` at runtime. */
    private fun formatSlots(value: String): List<String> =
        Regex("%(\\d+\\\$)?[a-zA-Z]").findAll(value).map { it.value }.toList()

    // -- the weekly-cap notice (Q-CAP-01) ------------------------------------------------------
    //
    // Only a cap that APPEARS or MOVES DOWN reaches a cleaner; the backend decides that. What these
    // pin is the half the backend cannot see: that all five locales carry the copy, that none of them
    // is the English string left in place, and that the body takes the CAP as an integer. A %s where
    // the code passes an Int is a format exception on the cleaner's lock screen and shows up nowhere
    // until it ships.

    @Test
    fun `every locale carries the weekly-cap copy`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            weeklyLimitKeys.forEach { key ->
                val value = valueOf(xml, key)
                assertNotNull("$locale/strings.xml is missing $key", value)
                assertTrue("$locale/strings.xml has a blank $key", value!!.isNotBlank())
            }
        }
    }

    @Test
    fun `the four translations of the weekly-cap copy are not the English string copied over`() {
        val english = weeklyLimitKeys.associateWith { valueOf(stringsXml("values"), it) }
        locales.drop(1).forEach { locale ->
            val xml = stringsXml(locale)
            weeklyLimitKeys.forEach { key ->
                assertTrue(
                    "$locale/strings.xml left $key in English",
                    valueOf(xml, key) != english[key],
                )
            }
        }
    }

    @Test
    fun `the weekly-cap body takes the cap as an integer and its title takes nothing`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            weeklyLimitKeys.forEach { key ->
                val expected = if (key.endsWith("_body")) listOf("%1${'$'}d") else emptyList()
                assertEquals(
                    "$locale/strings.xml passes the wrong format arguments to $key",
                    expected,
                    formatSlots(valueOf(xml, key)!!),
                )
            }
        }
    }
}
