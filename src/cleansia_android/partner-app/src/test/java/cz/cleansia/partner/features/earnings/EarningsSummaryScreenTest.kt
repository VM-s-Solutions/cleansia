package cz.cleansia.partner.features.earnings

import java.io.File
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/** This module has no Compose test harness, so what the Pay & Earnings cards bind is read as source. */
class EarningsSummaryScreenTest {

    /** A formatter built when the file loaded kept the first language for the life of the process, and had no year. */
    @Test
    fun `the period card names its days with the year, in the app's language`() {
        val card = functionBody("private fun PayPeriodCard(")
        assertTrue(card.contains("val locale = appLocale()"))
        assertTrue(card.contains("formatPeriod(stats.currentPayPeriodStart, stats.currentPayPeriodEnd, locale)"))
        assertTrue(card.contains("val end = calendarDate(stats.currentPayPeriodEnd) ?: return"))
        assertFalse("no formatter may be frozen at class load", code().contains("private val shortDateFormatter"))
    }

    /** The figure is this period's pay no invoice holds yet; it is invoiced when the period closes, not estimated. */
    @Test
    fun `the headline figure says when it is invoiced, and nothing promises a payout`() {
        val headline = functionBody("private fun HeadlineEarningsCard(")
        assertTrue(headline.contains("R.string.earnings_current_period"))
        assertTrue(
            headline.contains("formatShortDate(stats?.currentPayPeriodEnd, appLocale())") &&
                headline.contains("R.string.earnings_invoiced_on_close"),
        )
        assertFalse(headline.contains("latestInvoiceStatus"))
        assertFalse("Next payout leaves the cleaner apps", code().contains("payout", ignoreCase = true))
    }

    private fun code(): String = source.replace(Regex("""/\*[\s\S]*?\*/|//[^\n]*"""), "")

    private fun functionBody(marker: String): String {
        val text = code()
        val start = text.indexOf(marker)
        assertTrue("no `$marker` in EarningsSummaryScreen.kt", start >= 0)
        assertTrue("`$marker` must be unique for this extraction to mean anything", start == text.lastIndexOf(marker))
        val open = text.indexOf('{', start)
        var depth = 0
        for (i in open until text.length) {
            when (text[i]) {
                '{' -> depth++
                '}' -> if (--depth == 0) return text.substring(open, i + 1)
            }
        }
        error("unbalanced braces after `$marker`")
    }

    private val source: String = sequenceOf(
        File("."),
        File("partner-app"),
        File("src/cleansia_android/partner-app"),
    ).map { File(it, "src/main/java/cz/cleansia/partner/features/earnings/EarningsSummaryScreen.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?.replace("\r\n", "\n")
        ?: error("EarningsSummaryScreen.kt not found from working dir ${File(".").absolutePath}")
}
