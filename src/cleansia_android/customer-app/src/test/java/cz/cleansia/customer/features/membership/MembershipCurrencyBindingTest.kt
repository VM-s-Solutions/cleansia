package cz.cleansia.customer.features.membership

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * [MembershipViewModelTest] proves which code the Plus surfaces resolve; nothing there can see a
 * composable that still prints "Kč" on its own. The membership screens have no Compose harness, so
 * the call sites are pinned by source assertions, like the booking wizard's.
 */
class MembershipCurrencyBindingTest {

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    private val membershipDir = File(moduleDir, "src/main/java/cz/cleansia/customer/features/membership")

    private fun source(file: File): String =
        file.readText()
            .lines()
            .filterNot { it.trimStart().startsWith("//") || it.trimStart().startsWith("*") }
            .joinToString("\n")

    @Test
    fun `no membership screen prints the koruna or keeps its own formatter`() {
        val offenders = membershipDir.listFiles().orEmpty()
            .filter { it.extension == "kt" }
            .flatMap { file ->
                source(file).lines().withIndex()
                    .filter { (_, line) -> KORUNA.containsMatchIn(line) }
                    .map { (index, _) -> "${file.name}:${index + 1}" }
            }
        assertEquals("these lines label a plan price by hand", emptyList<String>(), offenders)
    }

    @Test
    fun `every plan price goes through the shared formatter with the resolved currency`() {
        val subscribe = source(File(membershipDir, "SubscribePlusScreen.kt")).replace(Regex("\\s+"), " ")
        assertTrue(
            "the subscribe screen no longer reads the resolved currency",
            subscribe.contains("viewModel.currencyCode.collectAsStateWithLifecycle()"),
        )
        assertTrue(
            "the plan price no longer goes through formatOrderPrice",
            subscribe.contains("formatOrderPrice(amount, currencyCode)"),
        )
        val card = source(File(membershipDir, "MembershipManagementCard.kt")).replace(Regex("\\s+"), " ")
        assertTrue(
            "the switch-to-annual dialog no longer formats with the resolved currency",
            card.contains("formatOrderPrice(yearlyPlan.price, currencyCode)"),
        )
    }

    /** The trial line takes a formatted amount; it used to hard-code "0 Kč" in five locales. */
    @Test
    fun `the trial price string takes a formatted amount in every locale`() {
        val resDir = File(moduleDir, "src/main/res")
        val locales = resDir.listFiles().orEmpty()
            .filter { it.name == "values" || it.name.startsWith("values-") }
            .map { File(it, "strings.xml") }
            .filter { it.isFile }
        assertTrue("no locale files found under ${resDir.absolutePath}", locales.size >= 5)

        locales.forEach { file ->
            val text = file.readText()
            val line = Regex("<string name=\"membership_hero_trial_price\">(.*?)</string>").find(text)?.groupValues?.get(1)
            assertTrue("${file.parentFile.name} lost membership_hero_trial_price", line != null)
            assertTrue("${file.parentFile.name} takes a formatted amount", line!!.contains("%1\$s"))
            assertTrue("${file.parentFile.name} takes the day count second", line.contains("%2\$d"))
            assertTrue("${file.parentFile.name} still names a currency: $line", !CURRENCY_LITERAL.containsMatchIn(line))
            assertTrue("${file.parentFile.name} still carries the dead per-month price", !text.contains("membership_plus_price_per_month"))
        }
    }

    private companion object {
        val KORUNA = Regex("""Kč|formatPriceCzk""")
        val CURRENCY_LITERAL = Regex("""CZK|Kč|EUR|€""")
    }
}
