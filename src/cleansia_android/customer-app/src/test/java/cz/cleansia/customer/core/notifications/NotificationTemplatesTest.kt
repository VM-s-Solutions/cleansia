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
 * `order.payment_confirmed` is produced by the Stripe webhook and by the customer confirming a
 * recurring occurrence, on neither of which has a cleaner seen the order.
 */
class NotificationTemplatesTest {
    @Test
    fun recurringPauseUsesTheExistingRecurringPreferenceAndEntersTheCustomerFeed() {
        val template = NotificationTemplates.templateFor("recurring.paused")
        assertNotNull(template)
        assertEquals(R.string.notification_recurring_paused_title, template?.titleRes)
        assertEquals(R.string.notification_recurring_paused_body, template?.bodyRes)
        assertEquals(NotificationCategoryDto.RecurringScheduled, template?.category)
        assertTrue(CustomerFeedEventKeys.contains("recurring.paused"))
    }

    @Test
    fun recurringPauseFormatsBodyWithoutArguments() {
        val context = mockk<Context>()
        val bodyRes = R.string.notification_recurring_paused_body
        every { context.getString(bodyRes) } returns "Schedule paused; renew Plus."
        for (args in listOf(emptyMap(), mapOf("orderNumber" to "MUST-NOT-APPEAR", "count" to "3"))) {
            assertEquals(
                "Schedule paused; renew Plus.",
                NotificationTemplates.formatBody(context, "recurring.paused", bodyRes, args),
            )
        }
    }

    @Test
    fun recurringPauseCopyExplainsInactivePlusAndRenewalInEveryLocale() {
        val requiredWords = mapOf(
            "values" to listOf("paused", "not active", "renew", "resume"),
            "values-cs" to listOf("pozastaven", "není aktivní", "obnovte", "pokračovat"),
            "values-sk" to listOf("pozastaven", "nie je aktívne", "obnovte", "pokračovať"),
            "values-uk" to listOf("призупинено", "неактивний", "поновіть", "відновити"),
            "values-ru" to listOf("приостановлено", "неактивен", "продлите", "возобновить"),
        )
        val cancellationWords = listOf("cancel", "zruš", "скасов", "скасован", "отмен")
        val english = stringsXml("values")
        for (locale in locales) {
            val xml = stringsXml(locale)
            val title = valueOf(xml, "notification_recurring_paused_title")
            val body = valueOf(xml, "notification_recurring_paused_body")
            assertNotNull("$locale pause title", title)
            assertNotNull("$locale pause body", body)
            assertTrue(title!!.isNotBlank())
            assertTrue(body!!.isNotBlank())
            assertEquals(emptyList<String>(), formatSlots(title))
            assertEquals(emptyList<String>(), formatSlots(body))
            assertTrue("$locale must name Plus", body.contains("Plus"))
            for (word in requiredWords.getValue(locale)) {
                assertTrue("$locale must explain $word", body.contains(word, ignoreCase = true))
            }
            for (word in cancellationWords) {
                assertFalse("$locale must not say visits were cancelled", "$title $body".contains(word, ignoreCase = true))
            }
            if (locale != "values") {
                assertFalse("$locale title left in English", title == valueOf(english, "notification_recurring_paused_title"))
                assertFalse("$locale body left in English", body == valueOf(english, "notification_recurring_paused_body"))
            }
        }
    }


    @Test
    fun `payment confirmation and saved legacy notifications share a template`() {
        val current = NotificationTemplates.templateFor("order.payment_confirmed")
        assertNotNull(current)
        assertEquals(NotificationTemplates.templateFor("order.confirmed"), current)
        assertEquals(NotificationCategoryDto.OrderUpdates, current?.category)
    }

    @Test
    fun `payment confirmation formats the order number for both wire keys`() {
        val context = mockk<Context>()
        val bodyRes = R.string.notification_order_payment_confirmed_body
        every { context.getString(bodyRes, "A-1042") } returns "Booking A-1042"
        listOf("order.payment_confirmed", "order.confirmed").forEach { key ->
            assertEquals(
                "Booking A-1042",
                NotificationTemplates.formatBody(context, key, bodyRes, mapOf("orderNumber" to "A-1042")),
            )
        }
    }

    @Test
    fun `payment confirmation and saved legacy taps require and open the booking`() {
        listOf("order.payment_confirmed", "order.confirmed").forEach { key ->
            assertEquals(Routes.OrderDetail("ord-7"), NotificationDeepLink.resolve(key, mapOf("orderId" to "ord-7")))
            assertNull(NotificationDeepLink.resolve(key, emptyMap()))
        }
    }

    @Test
    fun `payment confirmation copy exists with the same one argument in every locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            val title = valueOf(xml, "notification_order_payment_confirmed_title")
            val body = valueOf(xml, "notification_order_payment_confirmed_body")
            assertNotNull("$locale payment confirmation title", title)
            assertNotNull("$locale payment confirmation body", body)
            assertTrue(title!!.isNotBlank())
            assertTrue(body!!.isNotBlank())
            assertEquals(emptyList<String>(), formatSlots(title))
            assertEquals(listOf("%1${'$'}s"), formatSlots(body))
        }
    }

    @Test
    fun `current and persisted legacy payment notifications both remain in the customer feed`() {
        assertTrue(CustomerFeedEventKeys.contains("order.payment_confirmed"))
        assertTrue(CustomerFeedEventKeys.contains("order.confirmed"))
    }


    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /** The word each locale uses for the person, i.e. the claim being made. */
    private val cleanerWord = mapOf(
        "values" to "Cleaner",
        "values-cs" to "Uklízeč",
        "values-sk" to "Upratovač",
        "values-uk" to "Клінер",
        "values-ru" to "Клинер",
    )

    private val reminderKeys =
        listOf("notification_order_starting_soon_title", "notification_order_starting_soon_body")

    private val offerClosedKeys = listOf(
        "notification_order_preferred_offer_closed_title",
        "notification_order_preferred_offer_closed_body",
    )

    /**
     * ADR-0045 D7.3 — a decline and a silent lapse produce the same sentence, so any stem that
     * distinguishes them is the disclosure the single key exists to prevent. Both halves per locale:
     * the words for refusing, and the words for not answering.
     */
    private val outcomeStems = mapOf(
        "values" to listOf("declin", "refus", "reject", "turned down", "did not", "didn't", "no answer", "no response", "unanswer"),
        "values-cs" to listOf("odmít", "zamít", "neodpov", "bez odpov", "nereag"),
        "values-sk" to listOf("odmiet", "zamiet", "neodpov", "bez odpov", "nereag"),
        "values-uk" to listOf("відхил", "відмов", "не відповів", "не відповіла", "без відповід", "не відреаг"),
        "values-ru" to listOf("отклон", "отказ", "не ответил", "без ответа", "не отреаг"),
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

    @Test
    fun `templateFor maps the pre-cleaning reminder to the order-updates category`() {
        val template = NotificationTemplates.templateFor("order.starting_soon")

        assertEquals(R.string.notification_order_starting_soon_title, template?.titleRes)
        assertEquals(R.string.notification_order_starting_soon_body, template?.bodyRes)
        assertEquals(NotificationCategoryDto.OrderUpdates, template?.category)
    }

    @Test
    fun `formatBody substitutes the orderNumber into the reminder body`() {
        val context = mockk<Context>()
        every {
            context.getString(R.string.notification_order_starting_soon_body, "A-1042")
        } returns "Your booking #A-1042 starts in about an hour."

        val body = NotificationTemplates.formatBody(
            context,
            "order.starting_soon",
            R.string.notification_order_starting_soon_body,
            mapOf("orderId" to "ord-7", "orderNumber" to "A-1042"),
        )

        assertEquals("Your booking #A-1042 starts in about an hour.", body)
    }

    @Test
    fun `deep link resolves the reminder to the order detail`() {
        assertEquals(
            Routes.OrderDetail("ord-7"),
            NotificationDeepLink.resolve("order.starting_soon", mapOf("orderId" to "ord-7")),
        )
    }

    @Test
    fun `deep link returns null for the reminder without an orderId`() {
        assertNull(NotificationDeepLink.resolve("order.starting_soon", emptyMap()))
    }

    @Test
    fun `the reminder copy is translated in all five locales`() {
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
    fun `the four translations of the reminder copy are not the English string copied over`() {
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

    @Test
    fun `the reminder body takes the order number and the title takes nothing`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            assertEquals(
                "$locale/strings.xml does not pass exactly the order number to the reminder body",
                listOf("%1\$s"),
                formatSlots(valueOf(xml, "notification_order_starting_soon_body")!!),
            )
            assertEquals(
                "$locale/strings.xml puts a format slot on the argless reminder title",
                emptyList<String>(),
                formatSlots(valueOf(xml, "notification_order_starting_soon_title")!!),
            )
        }
    }

    @Test
    fun `templateFor maps the closed preferred offer to the order-updates category`() {
        val template = NotificationTemplates.templateFor("order.preferred_offer_closed")

        assertEquals(R.string.notification_order_preferred_offer_closed_title, template?.titleRes)
        assertEquals(R.string.notification_order_preferred_offer_closed_body, template?.bodyRes)
        assertEquals(NotificationCategoryDto.OrderUpdates, template?.category)
    }

    @Test
    fun `formatBody substitutes the orderNumber into the closed-offer body`() {
        val context = mockk<Context>()
        every {
            context.getString(R.string.notification_order_preferred_offer_closed_body, "A-1042")
        } returns "The cleaner request for booking #A-1042 has ended."

        val body = NotificationTemplates.formatBody(
            context,
            "order.preferred_offer_closed",
            R.string.notification_order_preferred_offer_closed_body,
            mapOf("orderId" to "ord-7", "orderNumber" to "A-1042"),
        )

        assertEquals("The cleaner request for booking #A-1042 has ended.", body)
    }

    @Test
    fun `deep link resolves the closed offer to the order detail`() {
        assertEquals(
            Routes.OrderDetail("ord-7"),
            NotificationDeepLink.resolve("order.preferred_offer_closed", mapOf("orderId" to "ord-7")),
        )
    }

    @Test
    fun `deep link returns null for the closed offer without an orderId`() {
        assertNull(NotificationDeepLink.resolve("order.preferred_offer_closed", emptyMap()))
    }

    @Test
    fun `the closed-offer copy is translated in all five locales`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            offerClosedKeys.forEach { key ->
                val value = valueOf(xml, key)
                assertNotNull("$locale/strings.xml is missing $key", value)
                assertTrue("$locale/strings.xml has a blank $key", value!!.isNotBlank())
            }
        }
    }

    @Test
    fun `the four translations of the closed-offer copy are not the English string copied over`() {
        val english = offerClosedKeys.associateWith { valueOf(stringsXml("values"), it) }
        locales.drop(1).forEach { locale ->
            val xml = stringsXml(locale)
            offerClosedKeys.forEach { key ->
                assertTrue(
                    "$locale/strings.xml left $key in English",
                    valueOf(xml, key) != english[key],
                )
            }
        }
    }

    @Test
    fun `the closed-offer body takes the order number and the title takes nothing`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            assertEquals(
                "$locale/strings.xml does not pass exactly the order number to the closed-offer body",
                listOf("%1\$s"),
                formatSlots(valueOf(xml, "notification_order_preferred_offer_closed_body")!!),
            )
            assertEquals(
                "$locale/strings.xml puts a format slot on the argless closed-offer title",
                emptyList<String>(),
                formatSlots(valueOf(xml, "notification_order_preferred_offer_closed_title")!!),
            )
        }
    }

    /**
     * The whole of ADR-0045 D7.3: one sentence covers a decline and a silent lapse. Checked in the
     * source language as well as the translations — a stem list that only matched the translations
     * would pass while English drifted into naming the outcome.
     */
    @Test
    fun `no closed-offer string says which way the offer ended in any locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            offerClosedKeys.forEach { key ->
                val value = valueOf(xml, key)!!
                outcomeStems.getValue(locale).forEach { stem ->
                    assertFalse(
                        "$locale/strings.xml tells the customer how the offer ended in $key: \"$value\"",
                        value.contains(stem, ignoreCase = true),
                    )
                }
            }
        }
    }

    /**
     * The client-first rule from the other side. The backend deliberately holds this key out of the
     * customer feed keyset until both apps carry copy, so a client-side keyset entry would count an
     * inbox row the feed never returns — the phantom badge, arrived at from here. Push copy first;
     * the keyset follows the server.
     */
    @Test
    fun `the closed offer renders as push but stays out of the feed keyset`() {
        assertNotNull(NotificationTemplates.templateFor("order.preferred_offer_closed"))
        assertFalse(CustomerFeedEventKeys.contains("order.preferred_offer_closed"))
    }

    /** The defect: the confirmation fires when the card clears, before any cleaner has seen the job. */
    @Test
    fun `no confirmation string claims a cleaner in any locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            listOf("notification_order_payment_confirmed_title", "notification_order_payment_confirmed_body").forEach { key ->
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

    private val noCleanerKeys = listOf(
        "notification_order_no_cleaner_refunded_title",
        "notification_order_no_cleaner_refunded_body",
    )

    @Test
    fun `formatBody renders the order number and the server-formatted amount into the no-cleaner body`() {
        val context = mockk<Context>()
        every {
            context.getString(R.string.notification_order_no_cleaner_refunded_body, "A-1042", "250 Kč")
        } returns "Nobody was able to take booking #A-1042, so we have refunded it in full and added 250 Kč credit towards your next clean. Sorry."

        val body = NotificationTemplates.formatBody(
            context,
            "order.no_cleaner_refunded",
            R.string.notification_order_no_cleaner_refunded_body,
            mapOf("orderId" to "ord-7", "orderNumber" to "A-1042", "amount" to "250 Kč"),
        )

        assertEquals(
            "Nobody was able to take booking #A-1042, so we have refunded it in full and added 250 Kč credit towards your next clean. Sorry.",
            body,
        )
    }

    /** An older server sends no amount; the sentence must still read, never with "null" in it. */
    @Test
    fun `formatBody renders the no-cleaner body with an empty amount when the server sent none`() {
        val context = mockk<Context>()
        every {
            context.getString(R.string.notification_order_no_cleaner_refunded_body, "A-1042", "")
        } returns "Nobody was able to take booking #A-1042, so we have refunded it in full and added  credit towards your next clean. Sorry."

        val body = NotificationTemplates.formatBody(
            context,
            "order.no_cleaner_refunded",
            R.string.notification_order_no_cleaner_refunded_body,
            mapOf("orderId" to "ord-7", "orderNumber" to "A-1042"),
        )

        assertFalse("an absent amount rendered as null: \"$body\"", body.contains("null"))
        assertTrue(body.contains("#A-1042"))
    }

    /** The amount is the no-cleaner key's alone; the plain cancellation keeps its single slot. */
    @Test
    fun `formatBody passes only the order number to the plain cancellation even when an amount arrives`() {
        val context = mockk<Context>()
        every {
            context.getString(R.string.notification_order_cancelled_body, "A-1042")
        } returns "Booking #A-1042 was cancelled."

        val body = NotificationTemplates.formatBody(
            context,
            "order.cancelled",
            R.string.notification_order_cancelled_body,
            mapOf("orderId" to "ord-7", "orderNumber" to "A-1042", "amount" to "250 Kč"),
        )

        assertEquals("Booking #A-1042 was cancelled.", body)
    }

    /**
     * The credit figure arrives formatted by the server in the credit's own currency, so the copy
     * takes it as a second slot and states no number and no currency of its own.
     */
    @Test
    fun `the no-cleaner body takes the order number and the amount and the title takes nothing in every locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            val body = valueOf(xml, "notification_order_no_cleaner_refunded_body")!!
            assertEquals(
                "$locale/strings.xml does not pass exactly the order number and the amount to the no-cleaner body",
                listOf("%1\$s", "%2\$s"),
                formatSlots(body),
            )
            assertFalse(
                "$locale/strings.xml bakes a figure into the no-cleaner body: \"$body\"",
                body.replace(Regex("%\\d+\\$[sd]"), "").contains(Regex("\\d")),
            )
            assertEquals(
                "$locale/strings.xml puts a format slot on the argless no-cleaner title",
                emptyList<String>(),
                formatSlots(valueOf(xml, "notification_order_no_cleaner_refunded_title")!!),
            )
        }
    }

    @Test
    fun `the four translations of the no-cleaner copy are not the English string copied over`() {
        val english = noCleanerKeys.associateWith { valueOf(stringsXml("values"), it) }
        locales.drop(1).forEach { locale ->
            val xml = stringsXml(locale)
            noCleanerKeys.forEach { key ->
                assertTrue(
                    "$locale/strings.xml left $key in English",
                    valueOf(xml, key) != english[key],
                )
            }
        }
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
}
