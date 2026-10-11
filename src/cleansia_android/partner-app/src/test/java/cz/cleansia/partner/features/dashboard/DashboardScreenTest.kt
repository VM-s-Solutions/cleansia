package cz.cleansia.partner.features.dashboard

import java.io.File
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

    @Test
    fun `the Help shortcut opens an e-mail to support`() {
        val onHelp = block(code(), "onHelp = ", '{', '}')
        listOf(
            "runCatching",
            "context.startActivity(",
            "Intent.ACTION_SENDTO",
            "Uri.parse(\"mailto:support@cleansia.cz\")",
        ).forEach { call -> assertTrue("the Help shortcut must use `$call`, got $onHelp", onHelp.contains(call)) }
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

    /** A koruna figure reads "Kč" whatever the phone's language, the rule the week card already follows. */
    @Test
    fun `a koruna figure is grouped and labelled Kc on an English and a Ukrainian phone`() {
        val original = Locale.getDefault()
        try {
            for (tag in listOf("en-US", "uk-UA")) {
                Locale.setDefault(Locale.forLanguageTag(tag))
                val figure = formatMoneyWithSymbol(3609.0, resolveCurrencySymbol("CZK"), fallback = "—")
                // uk groups digits with a no-break space, en with a comma the formatter turns into a space.
                assertEquals(tag, "3 609 Kč", figure.replace('\u00A0', ' '))
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
