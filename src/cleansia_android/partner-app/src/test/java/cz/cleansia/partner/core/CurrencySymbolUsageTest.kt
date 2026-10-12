package cz.cleansia.partner.core

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A currency's symbol belongs to the currency, not to the phone: looked up in the phone's locale it read
 * "CZK" on English and Ukrainian phones on Earnings, Invoices and Period pay while the dashboard read "Kč".
 * Every partner screen takes it from `CurrencySymbols`, and none guesses a currency from the locale.
 */
class CurrencySymbolUsageTest {

    @Test
    fun `no partner screen looks a currency or its symbol up in the phone's locale`() {
        val offenders = sources.flatMap { (path, text) ->
            text.lines().withIndex()
                .filter { (_, line) -> DEVICE_LOCALE_LOOKUP.containsMatchIn(line) }
                .map { (index, _) -> "$path:${index + 1}" }
        }
        assertEquals("these lines label money with the phone's locale", emptyList<String>(), offenders)
    }

    @Test
    fun `the money screens label their figures with CurrencySymbols`() {
        MONEY_SCREENS.forEach { path ->
            val text = sources.getValue(path)
            assertTrue("$path must take its symbol from CurrencySymbols.forCode", text.contains("CurrencySymbols.forCode("))
            assertFalse("$path keeps a private symbol lookup", text.contains("fun currencySymbol("))
        }
    }

    private val sources: Map<String, String> by lazy {
        val root = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
            .map { File(it, "src/main/java/cz/cleansia/partner") }
            .firstOrNull { it.isDirectory }
            ?: error("partner-app sources not found from working dir ${File(".").absolutePath}")
        val files = root.walkTopDown().filter { it.isFile && it.extension == "kt" }
            .associate { file ->
                val text = file.readText().replace("\r\n", "\n")
                file.relativeTo(root).invariantSeparatorsPath to text.replace(COMMENTS) { "\n".repeat(it.value.count { c -> c == '\n' }) }
            }
        MONEY_SCREENS.forEach { assertTrue("the walk must reach $it", it in files) }
        files
    }

    private companion object {
        val MONEY_SCREENS = listOf(
            "features/earnings/EarningsSummaryScreen.kt",
            "features/invoices/InvoicesListScreen.kt",
            "features/invoices/InvoiceDetailScreen.kt",
            "features/payroll/PeriodPayScreen.kt",
        )
        val DEVICE_LOCALE_LOOKUP = Regex(
            """getSymbol\(\s*Locale\.getDefault\(\)\s*\)|Currency\.getInstance\(\s*Locale\.getDefault\(\)\s*\)""",
        )
        val COMMENTS = Regex("""/\*[\s\S]*?\*/|(?<![:"])//[^\n]*""")
    }
}
