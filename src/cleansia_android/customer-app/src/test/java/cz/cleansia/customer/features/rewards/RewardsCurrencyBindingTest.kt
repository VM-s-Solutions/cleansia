package cz.cleansia.customer.features.rewards

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The tier floor (`MinimumOrderAmountForDiscount`) is a platform-default-currency number; the
 * ladder copy used to hard-code "CZK" / "Kč" around a bare integer in five locales. The rewards tab
 * has no Compose harness, so the binding is pinned by source and resource assertions.
 */
class RewardsCurrencyBindingTest {

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    @Test
    fun `the tier floor is formatted with the catalogue default currency`() {
        val flat = File(moduleDir, "src/main/java/cz/cleansia/customer/features/rewards/RewardsTab.kt")
            .readText()
            .lines()
            .filterNot { it.trimStart().startsWith("//") || it.trimStart().startsWith("*") }
            .joinToString(" ")
            .replace(Regex("\\s+"), " ")
        assertTrue(
            "the ladder no longer formats the floor with the catalogue currency",
            flat.contains("R.string.loyalty_discount_min_order, pct, formatOrderPrice(minOrder, currencyCode)"),
        )
        assertTrue(
            "the rewards tab no longer reads the catalogue default currency",
            flat.contains("viewModel.currencyCode.collectAsStateWithLifecycle()"),
        )
    }

    @Test
    fun `the floor string takes a formatted amount and names no currency in any locale`() {
        val resDir = File(moduleDir, "src/main/res")
        val locales = resDir.listFiles().orEmpty()
            .filter { it.name == "values" || it.name.startsWith("values-") }
            .map { File(it, "strings.xml") }
            .filter { it.isFile }
        assertTrue("no locale files found under ${resDir.absolutePath}", locales.size >= 5)

        locales.forEach { file ->
            val text = file.readText()
            val line = Regex("<string name=\"loyalty_discount_min_order\">(.*?)</string>").find(text)?.groupValues?.get(1)
            assertTrue("${file.parentFile.name} lost loyalty_discount_min_order", line != null)
            assertTrue("${file.parentFile.name} takes a formatted amount, not a bare number", line!!.contains("%2\$s"))
            assertTrue("${file.parentFile.name} still names a currency: $line", !CURRENCY_LITERAL.containsMatchIn(line))
            DEAD_PERK_STRINGS.forEach { key ->
                assertTrue("${file.parentFile.name} still carries the dead $key", !text.contains("name=\"$key\""))
            }
        }
    }

    private companion object {
        val CURRENCY_LITERAL = Regex("""CZK|Kč|EUR|€""")
        val DEAD_PERK_STRINGS = listOf(
            "loyalty_perks_discount_5_above_1000",
            "loyalty_perks_discount_10",
            "loyalty_perks_discount_12",
            "loyalty_perks_discount_15",
            "home_hero2_cta",
            "home_service_household_price",
            "home_service_upholstery_price",
            "home_service_windows_price",
        )
    }
}
