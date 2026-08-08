package cz.cleansia.customer.core.notifications

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.navigation.Routes
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
 * `order.cleaner_assigned` is the only event that may claim a cleaner is on the job:
 * `order.confirmed` is produced by the Stripe webhook and by the customer confirming a
 * recurring occurrence, on neither of which has a cleaner seen the order.
 */
class NotificationTemplatesTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /** The word each locale uses for the person, i.e. the claim being made. */
    private val cleanerWord = mapOf(
        "values" to "Cleaner",
        "values-cs" to "Uklízeč",
        "values-sk" to "Upratovač",
        "values-uk" to "Клінер",
        "values-ru" to "Клинер",
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    @Test
    fun `templateFor maps the assignment to the order-updates category`() {
        val template = NotificationTemplates.templateFor("order.cleaner_assigned")

        assertEquals(R.string.notification_cleaner_assigned_title, template?.titleRes)
        assertEquals(R.string.notification_cleaner_assigned_body, template?.bodyRes)
        assertEquals(NotificationCategoryDto.OrderUpdates, template?.category)
    }

    @Test
    fun `formatBody substitutes the orderNumber into the assignment body`() {
        val context = mockk<Context>()
        every {
            context.getString(R.string.notification_cleaner_assigned_body, "A-1042")
        } returns "A cleaner is assigned to your booking #A-1042."

        val body = NotificationTemplates.formatBody(
            context,
            "order.cleaner_assigned",
            R.string.notification_cleaner_assigned_body,
            mapOf("orderId" to "ord-7", "orderNumber" to "A-1042"),
        )

        assertEquals("A cleaner is assigned to your booking #A-1042.", body)
    }

    @Test
    fun `deep link resolves the assignment to the order detail`() {
        assertEquals(
            Routes.OrderDetail("ord-7"),
            NotificationDeepLink.resolve("order.cleaner_assigned", mapOf("orderId" to "ord-7")),
        )
    }

    @Test
    fun `deep link returns null for the assignment without an orderId`() {
        assertNull(NotificationDeepLink.resolve("order.cleaner_assigned", emptyMap()))
    }

    @Test
    fun `the assignment copy is translated in all five locales`() {
        val keys = listOf("notification_cleaner_assigned_title", "notification_cleaner_assigned_body")
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                val value = valueOf(xml, key)
                assertNotNull("$locale/strings.xml is missing $key", value)
                assertTrue("$locale/strings.xml has a blank $key", value!!.isNotBlank())
            }
        }
    }

    @Test
    fun `the four translations of the assignment copy are not the English string copied over`() {
        val keys = listOf("notification_cleaner_assigned_title", "notification_cleaner_assigned_body")
        val english = keys.associateWith { valueOf(stringsXml("values"), it) }
        locales.drop(1).forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                assertTrue(
                    "$locale/strings.xml left $key in English",
                    valueOf(xml, key) != english[key],
                )
            }
        }
    }

    @Test
    fun `the assignment body takes the order number`() {
        locales.forEach { locale ->
            val value = valueOf(stringsXml(locale), "notification_cleaner_assigned_body")!!
            assertTrue(
                "$locale/strings.xml dropped the order number from the assignment body",
                value.contains("%1\$s"),
            )
        }
    }

    /** The defect: the confirmation fires when the card clears, before any cleaner has seen the job. */
    @Test
    fun `no confirmation string claims a cleaner in any locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            listOf("notification_order_confirmed_title", "notification_order_confirmed_body").forEach { key ->
                val value = valueOf(xml, key)!!
                assertFalse(
                    "$locale/strings.xml still claims a cleaner in $key: \"$value\"",
                    value.contains(cleanerWord.getValue(locale), ignoreCase = true),
                )
            }
        }
    }

    @Test
    fun `the claim moved to the assignment title rather than being deleted`() {
        locales.forEach { locale ->
            val value = valueOf(stringsXml(locale), "notification_cleaner_assigned_title")!!
            assertTrue(
                "$locale/strings.xml no longer names the cleaner on the assignment title: \"$value\"",
                value.contains(cleanerWord.getValue(locale), ignoreCase = true),
            )
        }
    }

    /**
     * The badge counts feed rows. A key with a template but no row inflates it, which is the
     * hazard `NotificationFeedEventKeys`' own doc-comment states, reached from this side.
     */
    @Test
    fun `every feed key renders and the badge bumps off the keyset`() {
        CustomerFeedEventKeys.all.forEach { key ->
            assertNotNull("feed key $key has no template — its row would be dropped unrendered", NotificationTemplates.templateFor(key))
        }

        val service = File(
            resDir.parentFile,
            "java/cz/cleansia/customer/core/notifications/CleansiaFirebaseMessagingService.kt",
        )
        assertTrue("CleansiaFirebaseMessagingService.kt not found next to res/", service.isFile)
        assertTrue(
            "the badge bump is no longer gated on the feed keyset",
            service.readText().contains("if (CustomerFeedEventKeys.contains(eventKey))"),
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
}
