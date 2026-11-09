package cz.cleansia.partner.features.dashboard

import java.io.File
import java.text.DecimalFormatSymbols
import java.util.Locale
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The dashboard's composables have no seam and this module has no Compose test harness, so what they
 * bind is read as source; the money helpers they share are called directly.
 */
class DashboardScreenTest {

    /** With no mail app on the phone the tap did nothing; now it copies the address and says so, as the customer Help does. */
    @Test
    fun `the Help shortcut opens an e-mail to support, or copies the address and says so`() {
        val onHelp = block(code(), "onHelp = ", '{', '}')
        listOf(
            "Intent.ACTION_SENDTO",
            "Uri.parse(\"mailto:\${CleansiaWeb.CONTACT_EMAIL}\")",
        ).forEach { call -> assertTrue("the Help shortcut must use `$call`, got $onHelp", onHelp.contains(call)) }
        assertTrue(
            "a mail intent no app takes must copy the address and tell the cleaner, got $onHelp",
            Regex("""if \(!context\.openOrCopy\(\w+, CleansiaWeb\.CONTACT_EMAIL\)\) viewModel\.onHelpEmailUnavailable\(\)""")
                .containsMatchIn(onHelp),
        )
        assertFalse("a refused intent must not be swallowed, got $onHelp", onHelp.contains("runCatching"))
    }

    @Test
    fun `the next job's when-line is written in the cleaner's language`() {
        val whenLine = functionBody("private fun nextJobWhenLine(")
        listOf(
            "R.string.urgency_in_minutes",
            "R.string.urgency_in_hours_minutes",
            "R.string.day_today",
            "R.string.day_tomorrow",
        ).forEach { key -> assertTrue("nextJobWhenLine must render `$key`", whenLine.contains(key)) }
        val english = Regex("\"([^\"]*)\"").findAll(whenLine)
            .map { it.groupValues[1] }
            .filter { Regex("""\b(?:In|Today|Tomorrow|min)\b""").containsMatchIn(it) }
            .toList()
        assertEquals("nextJobWhenLine still hard-codes English", emptyList<String>(), english)
    }

    @Test
    fun `the when-line says tomorrow only for the next calendar day`() {
        val whenLine = functionBody("private fun nextJobWhenLine(")
        assertTrue(
            "nextJobWhenLine must decide Tomorrow with startsTomorrow(instant, now, zone)",
            whenLine.contains("startsTomorrow(instant, now, zone)"),
        )
        assertFalse("nextJobWhenLine must not count whole days for Tomorrow", whenLine.contains("toDays()"))
    }

    /** An unread or failed upcoming list is not a free day: the greeting then shows the date alone. */
    @Test
    fun `the greeting omits its today line while the day's jobs are unknown`() {
        val screen = code()
        assertTrue(
            "the count must come from the nullable upcoming list",
            screen.contains("todaysJobCount(upcoming, LocalDate.now(), ZoneId.systemDefault())"),
        )
        assertTrue("the greeting must take a nullable count", screen.contains("todaysJobsCount: Int?,"))
        assertTrue("the greeting must be handed the count as it is", screen.contains("todaysJobsCount = todaysJobsCount,"))
        val bar = functionBody("private fun CompactGreetingBar(")
        assertTrue("the greeting lost its today line", bar.contains("todaysStateLine(todaysJobsCount)"))
        assertTrue(
            "the today line must render only inside if (todaysJobsCount != null)",
            block(bar, "if (todaysJobsCount != null) {", '{', '}').contains("todaysStateLine(todaysJobsCount)"),
        )
    }

    @Test
    fun `the pay-period and last-month figures carry the currency symbol`() {
        val payPeriod = functionBody("private fun PayPeriodCard(")
        assertTrue(
            "PayPeriodCard must resolve the stats' currency symbol",
            payPeriod.contains("val currencySymbol = remember(stats.currencyCode) { resolveCurrencySymbol(stats.currencyCode) }"),
        )
        assertTrue(
            "PayPeriodCard must format the period's earnings with that symbol",
            payPeriod.contains("formatMoneyWithSymbol(stats.currentPeriodEarnings, currencySymbol, fallback = \"—\")"),
        )
        val lastMonth = functionBody("private fun LastMonthCard(")
        assertTrue(
            "LastMonthCard must resolve the stats' currency symbol",
            lastMonth.contains("val currencySymbol = remember(stats?.currencyCode) { resolveCurrencySymbol(stats?.currencyCode) }"),
        )
        assertTrue(
            "LastMonthCard must format last month's earnings with that symbol",
            lastMonth.contains("formatMoneyWithSymbol(stats?.lastMonthEarnings, currencySymbol, fallback = \"—\")"),
        )
        assertFalse("the bare figure formatter must be gone", code().contains("fun formatMoney("))
    }

    /**
     * The period's days arrive as calendar dates. Read as instants they named the wrong day west of Greenwich, and a
     * date-only value failed the parse and hid the card. Payouts are manual, so no payout date is promised (R4).
     */
    @Test
    fun `the pay-period card reads calendar days and says when the figure is invoiced`() {
        val payPeriod = functionBody("private fun PayPeriodCard(")
        assertTrue(payPeriod.contains("val start = calendarDate(stats.currentPayPeriodStart) ?: return"))
        assertTrue(payPeriod.contains("val end = calendarDate(stats.currentPayPeriodEnd) ?: return"))
        assertFalse("the period's days must not pass through a time zone", Regex("""Instant|ZoneId""").containsMatchIn(payPeriod))
        assertTrue("the figure must carry the label it has on Pay & Earnings", payPeriod.contains("R.string.earnings_current_period"))
        assertTrue(
            "the hint must name the period's last day in the app's language",
            payPeriod.contains("formatShortDate(stats.currentPayPeriodEnd, appLocale())") &&
                payPeriod.contains("R.string.earnings_invoiced_on_close"),
        )
        assertFalse("Next payout leaves the cleaner apps", code().contains("payout", ignoreCase = true))
    }

    /** A koruna figure reads "Kč" whatever the phone's language, the rule the week card already follows. */
    @Test
    fun `a koruna figure is grouped and labelled Kc on an English and a Ukrainian phone`() {
        val original = Locale.getDefault()
        try {
            for (tag in listOf("en-US", "uk-UA")) {
                Locale.setDefault(Locale.forLanguageTag(tag))
                val figure = formatMoneyWithSymbol(3609.0, resolveCurrencySymbol("CZK"), fallback = "—")
                // uk groups digits with a no-break space, narrow on some JDKs, and en with a comma the
                // formatter turns into a space.
                assertEquals(tag, "3 609 Kč", figure.replace('\u00A0', ' ').replace('\u202F', ' '))
            }
        } finally {
            Locale.setDefault(original)
        }
    }

    /** Whole units in each language the app ships, an English comma drawn as a space; nothing earned reads the fallback. */
    @Test
    fun `a dashboard figure is a whole unit and an English comma is drawn as a space`() {
        val original = Locale.getDefault()
        try {
            for (tag in listOf("en-US", "cs-CZ", "sk-SK", "uk-UA", "ru-RU")) {
                val locale = Locale.forLanguageTag(tag)
                Locale.setDefault(locale)
                val group = DecimalFormatSymbols.getInstance(locale).groupingSeparator.let { if (it == ',') ' ' else it }
                assertEquals(tag, "1${group}276 Kč", formatMoneyWithSymbol(1275.5, "Kč", fallback = "—"))
                listOf(null, 0.0, -5.0).forEach { assertEquals(tag, "—", formatMoneyWithSymbol(it, "Kč", fallback = "—")) }
            }
            Locale.setDefault(Locale.US)
            assertEquals("12 345 Kč", formatMoneyWithSymbol(12_345.4, "Kč", fallback = "—"))
        } finally {
            Locale.setDefault(original)
        }
    }

    /** As on iOS and in the docs, a figure the server names no currency for carries no symbol, not the phone's. */
    @Test
    fun `a missing currency code shows no symbol on any phone`() {
        val original = Locale.getDefault()
        try {
            for (tag in listOf("en-US", "uk-UA", "cs-CZ")) {
                Locale.setDefault(Locale.forLanguageTag(tag))
                assertEquals(tag, "", resolveCurrencySymbol(null))
                assertEquals(tag, "", resolveCurrencySymbol(" "))
            }
        } finally {
            Locale.setDefault(original)
        }
    }

    /** The list scrolls under the status bar from the greeting down, so it wears the shared fade. */
    @Test
    fun `the dashboard fades its list under the status bar`() {
        val screen = code()
        assertTrue("the dashboard must hold its list's state", screen.contains("val listState = rememberLazyListState()"))
        val list = block(screen, "LazyColumn(", '(', ')')
        assertTrue("the LazyColumn must be driven by that state", list.contains("state = listState"))
        assertTrue(
            "the LazyColumn must chain .statusBarFade(listState)",
            Regex("""\.statusBarFade\(listState\)""").containsMatchIn(list),
        )
    }

    private fun code(): String = source.replace(Regex("""/\*[\s\S]*?\*/|//[^\n]*"""), "")

    private fun functionBody(marker: String): String {
        val text = code()
        val start = text.indexOf(marker)
        assertTrue("no `$marker` in DashboardScreen.kt", start >= 0)
        assertTrue("`$marker` must be unique for this extraction to mean anything", start == text.lastIndexOf(marker))
        return block(text, marker, '{', '}')
    }

    /** The bracketed block that opens first after [marker]. */
    private fun block(text: String, marker: String, opener: Char, closer: Char): String {
        val start = text.indexOf(marker)
        assertTrue("no `$marker` in DashboardScreen.kt", start >= 0)
        val open = text.indexOf(opener, start)
        var depth = 0
        for (i in open until text.length) {
            when (text[i]) {
                opener -> depth++
                closer -> if (--depth == 0) return text.substring(open, i + 1)
            }
        }
        error("unbalanced `$opener` after `$marker`")
    }

    private val source: String = sequenceOf(
        File("."),
        File("partner-app"),
        File("src/cleansia_android/partner-app"),
    ).map { File(it, "src/main/java/cz/cleansia/partner/features/dashboard/DashboardScreen.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?.replace("\r\n", "\n")
        ?: error("DashboardScreen.kt not found from working dir ${File(".").absolutePath}")
}
