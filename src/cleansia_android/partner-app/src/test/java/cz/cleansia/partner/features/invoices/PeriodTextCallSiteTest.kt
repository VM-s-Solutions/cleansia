package cz.cleansia.partner.features.invoices

import java.io.File
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The server's period label is English ("Nov 03 - Nov 16, 2026") whatever the cleaner reads. This module has no
 * Compose test harness, so the three screens that printed it are read as source; formatPeriod itself is ShortDateTest's.
 */
class PeriodTextCallSiteTest {

    @Test
    fun `the invoice list, the invoice detail and period pay print the period from its days in the app's language`() {
        mapOf(
            "features/invoices/InvoicesListScreen.kt" to "formatPeriod(invoice.payPeriodStartDate, invoice.payPeriodEndDate, appLocale())",
            "features/invoices/InvoiceDetailScreen.kt" to "formatPeriod(invoice.payPeriodStartDate, invoice.payPeriodEndDate, appLocale())",
            "features/payroll/PeriodPayScreen.kt" to "formatPeriod(summary.payPeriodStartDate, summary.payPeriodEndDate, appLocale())",
        ).forEach { (path, call) ->
            val code = code(path)
            assertTrue("$path must render `$call`", code.contains(call))
            assertFalse("$path must not print the server's label", code.contains("payPeriodLabel"))
        }
    }

    private fun code(path: String): String = sequenceOf(
        File("."),
        File("partner-app"),
        File("src/cleansia_android/partner-app"),
    ).map { File(it, "src/main/java/cz/cleansia/partner/$path") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?.replace(Regex("""/\*[\s\S]*?\*/|//[^\n]*"""), "")
        ?: error("$path not found from working dir ${File(".").absolutePath}")
}
