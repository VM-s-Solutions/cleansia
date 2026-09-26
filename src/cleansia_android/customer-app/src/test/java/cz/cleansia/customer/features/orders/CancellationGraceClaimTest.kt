package cz.cleansia.customer.features.orders

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Owner ruling 2026-09-24: after booking, cancelling is free for 15 minutes, and for 60 for an entitled
 * Plus member. Guests and first-time customers get the standard 15 — the first-time 60 the copy once
 * implied is gone from the server, so it must not come back in any locale. The figures are read from
 * `BookingPolicy` itself, the same way the web's claim spec reads them.
 */
class CancellationGraceClaimTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    private val solutionDir: File = generateSequence(moduleDir.absoluteFile) { it.parentFile }
        .firstOrNull { File(it, "Cleansia.Api.sln").isFile }
        ?: error("Cleansia.Api.sln not found above ${moduleDir.absolutePath}")

    private val standard = policyMinutes("OopsWindowMinutesStandard")
    private val plus = policyMinutes("OopsWindowMinutesPlus")

    private fun policyMinutes(name: String): Int {
        val source = File(solutionDir, "Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs").readText()
        return Regex("public\\s+const\\s+int\\s+$name\\s*=\\s*(\\d+)\\s*;").find(source)?.groupValues?.get(1)?.toInt()
            ?: error("BookingPolicy.$name not found — the parser needs updating")
    }

    private data class Sides(val plus: String, val standard: String)

    /** "15 minutes … — 60 minutes with Cleansia Plus": the side that names Plus is the Plus figure's. */
    private fun aroundDash(value: String): Sides {
        val parts = value.split("—")
        return Sides(
            plus = parts.filter { it.contains("Cleansia Plus") }.joinToString(" "),
            standard = parts.filterNot { it.contains("Cleansia Plus") }.joinToString(" "),
        )
    }

    /** "60 minutes …, instead of 15 minutes": a Plus perk, so what it replaces is the standard figure. */
    private fun aroundInsteadOf(value: String): Sides {
        val match = Regex("instead of|namiesto|místo|замість|вместо", RegexOption.IGNORE_CASE).find(value)
            ?: return Sides(plus = "", standard = value)
        return Sides(plus = value.substring(0, match.range.first), standard = value.substring(match.range.first))
    }

    /** Every sentence that states the grace, and how to tell whose figure each side is. */
    private val graceClaims: List<Pair<String, (String) -> Sides>> = listOf(
        "booking_cancel_grace_note" to ::aroundDash,
        "help_faq_a1" to ::aroundDash,
        "membership_perk_grace_desc" to ::aroundInsteadOf,
    )

    private val minuteFigure = Regex("(\\d+)\\s*-?\\s*(?:minutes?|minut|minút|хвилин|минут)", RegexOption.IGNORE_CASE)
    private val cancelStems = listOf("cancel", "zruš", "storn", "скасув", "скасов", "отмен")
    private val firstTimeClaim = Regex(
        "first[-\\s]time|first (?:booking|order)|new customer|prvn\\S* (?:objedn|zákazn|úklid)|" +
            "prv\\S* (?:objedn|zákazn|upratov)|nov\\S* zákazn|перш\\S* (?:замовлен|прибиран)|нов\\S* клієнт|" +
            "перв\\S* (?:заказ|уборк)|нов\\S* клиент",
        RegexOption.IGNORE_CASE,
    )

    private fun minuteFigures(text: String): List<Int> = minuteFigure.findAll(text).map { it.groupValues[1].toInt() }.toList()

    @Test
    fun `the two figures are read off the server and differ`() {
        assertTrue(standard > 0)
        assertTrue(plus > standard)
    }

    @Test
    fun `every grace sentence states each figure on the side it belongs to, in every locale`() {
        locales.forEach { locale ->
            val strings = strings(locale)
            graceClaims.forEach { (key, sidesOf) ->
                val value = strings[key] ?: error("$locale/$key is missing")
                val sides = sidesOf(value)
                assertEquals("$locale/$key Plus side — $value", listOf(plus), minuteFigures(sides.plus))
                assertEquals("$locale/$key standard side — $value", listOf(standard), minuteFigures(sides.standard))
            }
        }
    }

    @Test
    fun `the Plus perk title names no figure of its own`() {
        locales.forEach { locale ->
            val title = strings(locale)["membership_perk_grace_title"] ?: error("$locale title is missing")
            assertEquals("$locale/membership_perk_grace_title — $title", emptyList<Char>(), title.filter { it.isDigit() }.toList())
        }
    }

    /** The sheet's line is the customer's OWN grace, so it carries the server's figure and no other. */
    @Test
    fun `the cancel sheet's grace note carries the server figure and no number of its own`() {
        locales.forEach { locale ->
            val items = plurals(locale)["order_cancel_fee_grace_note"] ?: error("$locale/order_cancel_fee_grace_note is missing")
            assertTrue("$locale has no <item> for the grace note", items.isNotEmpty())
            items.forEach { item ->
                assertTrue("$locale grace note lost its %1\$d — $item", item.contains("%1\$d"))
                assertEquals(
                    "$locale grace note bakes a figure in — $item",
                    emptyList<Char>(),
                    item.replace("%1\$d", "").filter { it.isDigit() }.toList(),
                )
            }
        }
    }

    /** Minute figures beside a cancel word that are not the grace: how long the cleaner waits at the door. */
    private val notGraceMinutes = setOf("help_faq_a2")

    /** Known grace claims that name cancelling, so the sweep must find them by its own stems. */
    private val sweptGraceClaims = listOf("help_faq_a1", "membership_perk_grace_desc")

    @Test
    fun `no string states a grace other than the two, or a first-time grace`() {
        locales.forEach { locale ->
            val graceSentences = strings(locale).filter { (key, value) ->
                key !in notGraceMinutes &&
                    minuteFigures(value).isNotEmpty() &&
                    cancelStems.any { value.contains(it, ignoreCase = true) }
            }
            assertTrue(
                "$locale: the sweep missed a known grace claim, found only ${graceSentences.keys}",
                graceSentences.keys.containsAll(sweptGraceClaims),
            )
            graceSentences.forEach { (key, value) ->
                assertEquals("$locale/$key — $value", emptyList<Int>(), minuteFigures(value).filter { it != standard && it != plus })
                assertTrue("$locale/$key claims a first-time grace — $value", !firstTimeClaim.containsMatchIn(value))
            }
        }
    }

    /** The grace tier sits above the sheet's own grace note, and that grace can run for an hour. */
    @Test
    fun `the grace tier title claims no moment of booking`() {
        val justBooked = Regex("moments ago|malou chv|щойно|только что", RegexOption.IGNORE_CASE)
        locales.forEach { locale ->
            val title = strings(locale)["order_cancel_fee_oops"] ?: error("$locale/order_cancel_fee_oops is missing")
            assertTrue("$locale/order_cancel_fee_oops says the booking was just made — $title", !justBooked.containsMatchIn(title))
            assertEquals("$locale/order_cancel_fee_oops names a figure — $title", emptyList<Char>(), title.filter { it.isDigit() }.toList())
        }
    }

    @Test
    fun `every surface that states the grace renders it`() {
        listOf(
            "features/booking/ConfirmStep.kt" to "R.string.booking_cancel_grace_note",
            "features/profile/HelpSupportScreen.kt" to "R.string.help_faq_a1",
            "features/membership/SubscribePlusScreen.kt" to "R.string.membership_perk_grace_title",
            "features/membership/SubscribePlusScreen.kt" to "R.string.membership_perk_grace_desc",
            "features/orders/CancelOrderSheet.kt" to "callout.graceMinutes",
            "features/orders/CancelOrderSheet.kt" to "pluralStringResource(R.plurals.order_cancel_fee_grace_note, minutes, minutes)",
        ).forEach { (file, reference) ->
            assertTrue("$file no longer renders $reference", source(file).contains(reference))
        }
    }

    private fun source(relative: String): String =
        File(moduleDir, "src/main/java/cz/cleansia/customer/$relative")
            .also { assertTrue("$relative not found", it.isFile) }
            .readText()

    private fun xml(locale: String): String {
        val file = File(moduleDir, "src/main/res/$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    private fun strings(locale: String): Map<String, String> =
        Regex("<string name=\"([^\"]+)\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .findAll(xml(locale))
            .associate { it.groupValues[1] to it.groupValues[2] }

    private fun plurals(locale: String): Map<String, List<String>> =
        Regex("<plurals name=\"([^\"]+)\">(.*?)</plurals>", RegexOption.DOT_MATCHES_ALL)
            .findAll(xml(locale))
            .associate { match ->
                match.groupValues[1] to Regex("<item quantity=\"[^\"]+\">(.*?)</item>", RegexOption.DOT_MATCHES_ALL)
                    .findAll(match.groupValues[2])
                    .map { it.groupValues[1] }
                    .toList()
            }
}
