package cz.cleansia.customer.features.booking

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * [BookingViewModelTest] proves which code the wizard resolves and
 * [cz.cleansia.customer.core.catalog.CatalogRepositoryTest] proves where the catalogue's comes from;
 * neither can see whether a screen still hands `formatOrderPrice` a null, which it reads as CZK. The
 * booking steps have no Compose harness, so the call sites are pinned by source assertions.
 */
class BookingCurrencyBindingTest {

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    private val bookingDir = File(moduleDir, "src/main/java/cz/cleansia/customer/features/booking")

    /** Comments are stripped: a prose mention of `formatOrderPrice(x, null)` is not a call site. */
    private fun source(file: File): String =
        file.readText()
            .lines()
            .filterNot { it.trimStart().startsWith("//") || it.trimStart().startsWith("*") }
            .joinToString("\n")

    private fun source(name: String): String =
        File(bookingDir, name).also { assertTrue("$name not found at ${it.absolutePath}", it.isFile) }.let(::source)

    @Test
    fun `no wizard amount is formatted with a null currency`() {
        val offenders = bookingDir.listFiles().orEmpty()
            .filter { it.extension == "kt" }
            .flatMap { file ->
                source(file).lines().withIndex()
                    .filter { (_, line) -> NULL_CURRENCY.containsMatchIn(line) }
                    .map { (index, _) -> "${file.name}:${index + 1}" }
            }
        assertEquals("these amounts fall back to CZK by construction", emptyList<String>(), offenders)
    }

    @Test
    fun `the service row prices through the formatter rather than a CZK string`() {
        val flat = source("ServicesStep.kt").replace(Regex("\\s+"), " ")
        assertTrue(
            "the base price is no longer formatted with the catalogue currency",
            flat.contains("R.string.booking_price_from, formatOrderPrice(service.basePrice, currencyCode)"),
        )
        assertTrue(
            "the per-room price is no longer formatted with the catalogue currency",
            flat.contains("R.string.booking_price_per_room, formatOrderPrice(service.perRoomPrice, currencyCode)"),
        )
    }

    @Test
    fun `the price strings carry no currency of their own in any locale`() {
        val resDir = File(moduleDir, "src/main/res")
        val locales = resDir.listFiles().orEmpty()
            .filter { it.name == "values" || it.name.startsWith("values-") }
            .map { File(it, "strings.xml") }
            .filter { it.isFile }
        assertTrue("no locale files found under ${resDir.absolutePath}", locales.size >= 5)

        locales.forEach { file ->
            val text = file.readText()
            listOf("booking_price_from", "booking_price_per_room").forEach { key ->
                val line = Regex("<string name=\"$key\">(.*?)</string>").find(text)?.groupValues?.get(1)
                assertTrue("${file.parentFile.name} lost $key", line != null)
                assertTrue("${file.parentFile.name}/$key takes a formatted amount, not a bare number", line!!.contains("%1\$s"))
                assertTrue("${file.parentFile.name}/$key still names a currency: $line", !CURRENCY_LITERAL.containsMatchIn(line))
            }
        }
    }

    @Test
    fun `the confirm step labels every row with the resolved display currency`() {
        val confirm = source("ConfirmStep.kt").replace(Regex("\\s+"), " ")
        assertTrue(
            "the confirm step no longer reads the wizard's resolved currency",
            confirm.contains("bookingVm.displayCurrencyCode.collectAsStateWithLifecycle()"),
        )
    }

    private companion object {
        val NULL_CURRENCY = Regex("""formatOrderPrice\([^)]*,\s*null\s*\)""")
        val CURRENCY_LITERAL = Regex("""CZK|Kč|EUR|€""")
    }
}
