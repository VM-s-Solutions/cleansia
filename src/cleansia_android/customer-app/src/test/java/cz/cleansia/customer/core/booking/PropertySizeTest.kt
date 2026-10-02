package cz.cleansia.customer.core.booking

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Every basket validator refuses a home above `BookingPolicy.MaxRooms` / `MaxBathrooms`
 * (`order.size_exceeds_maximum`), so a stepper that goes one further offers a booking that cannot be
 * made. Read from the policy itself, so the two cannot drift apart unnoticed.
 */
class PropertySizeTest {

    private val solutionDir: File = generateSequence(File(".").absoluteFile) { it.parentFile }
        .firstOrNull { File(it, "Cleansia.Api.sln").isFile }
        ?: error("Cleansia.Api.sln not found above ${File(".").absolutePath}")

    private fun policyInt(name: String): Int {
        val source = File(solutionDir, "Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs").readText()
        return Regex("public\\s+const\\s+int\\s+$name\\s*=\\s*(\\d+)\\s*;").find(source)?.groupValues?.get(1)?.toInt()
            ?: error("BookingPolicy.$name not found — the parser needs updating")
    }

    @Test
    fun `the room cap is the server's`() {
        assertEquals(policyInt("MaxRooms"), PropertySize.MAX_ROOMS)
    }

    @Test
    fun `the bathroom cap is the server's`() {
        assertEquals(policyInt("MaxBathrooms"), PropertySize.MAX_BATHROOMS)
    }

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    private fun source(relative: String): String =
        File(moduleDir, "src/main/java/cz/cleansia/customer/$relative").readText()

    /**
     * The caption states the caps, so they are its placeholders: a number written into a translation
     * stays behind the first time the policy moves.
     */
    @Test
    fun `the size caption states the caps through its placeholders in all five locales`() {
        listOf("values", "values-cs", "values-sk", "values-uk", "values-ru").forEach { locale ->
            val xml = File(moduleDir, "src/main/res/$locale/strings.xml").readText()
            val value = Regex("<string name=\"booking_size_limit_caption\"[^>]*>(.*?)</string>")
                .find(xml)?.groupValues?.get(1)
                ?: error("$locale/booking_size_limit_caption is missing")
            assertTrue("$locale lost the rooms placeholder — $value", value.contains("%1\$d"))
            assertTrue("$locale lost the bathrooms placeholder — $value", value.contains("%2\$d"))
            assertTrue(
                "$locale names a number of its own — $value",
                value.replace("%1\$d", "").replace("%2\$d", "").none { it.isDigit() },
            )
        }
    }

    @Test
    fun `both booking flows state the caps under their size steppers`() {
        listOf("features/booking/ServicesStep.kt", "features/recurring/CreateRecurringScreen.kt").forEach { file ->
            val flat = source(file).replace(Regex("\\s+"), " ")
            assertTrue(
                "$file no longer states the caps under its steppers",
                flat.contains("R.string.booking_size_limit_caption, PropertySize.MAX_ROOMS, PropertySize.MAX_BATHROOMS"),
            )
        }
    }

    /**
     * Owner ruling 2026-10-02: a bathroom is "ванна кімната" in Ukrainian and "ванная" in Russian. Bare
     * "ванна", "ванни" or "ванны" is a bathtub, which is what the steppers, the summary and the order
     * detail all counted before.
     */
    @Test
    fun `Ukrainian and Russian count bathrooms, not bathtubs`() {
        val expected = mapOf(
            "values-uk" to mapOf(
                "one" to "%1\$d ванна кімната",
                "few" to "%1\$d ванні кімнати",
                "many" to "%1\$d ванних кімнат",
                "other" to "%1\$d ванні кімнати",
            ),
            "values-ru" to mapOf(
                "one" to "%1\$d ванная",
                "few" to "%1\$d ванные",
                "many" to "%1\$d ванных",
                "other" to "%1\$d ванные",
            ),
        )
        expected.forEach { (locale, items) ->
            val xml = File(moduleDir, "src/main/res/$locale/strings.xml").readText()
            val body = Regex("<plurals name=\"booking_bath_short\">(.*?)</plurals>", RegexOption.DOT_MATCHES_ALL)
                .find(xml)?.groupValues?.get(1)
                ?: error("$locale/booking_bath_short is missing")
            val declared = Regex("<item quantity=\"([^\"]+)\">(.*?)</item>").findAll(body)
                .associate { it.groupValues[1] to it.groupValues[2] }
            assertEquals("$locale/booking_bath_short", items, declared)
        }
    }

    /**
     * "1 ванна кімната" is wider than a 360dp phone leaves the bathrooms counter, so its label is the
     * part that gives: weighted, so both steps keep their place, and wrapping onto a second line.
     */
    @Test
    fun `a counter label too wide for its pill wraps instead of pushing the plus out`() {
        val counter = source("features/booking/ServicesStep.kt")
            .substringAfter("private fun CompactCounter(")
            .substringBefore("private fun CounterStep(")
        assertTrue("the label is no longer weighted", counter.contains("Modifier.weight(1f, fill = false)"))
        assertTrue("the label no longer wraps onto two lines", counter.contains("maxLines = 2"))
    }

    /** The one-off flow floors at 1 like the recurring one floors at 0: a minus that cannot move looks dead. */
    @Test
    fun `the one-off minus stops at one room and one bathroom`() {
        val services = source("features/booking/ServicesStep.kt")
        assertTrue("the rooms minus no longer stops at 1", services.contains("range = 1..PropertySize.MAX_ROOMS"))
        assertTrue("the bathrooms minus no longer stops at 1", services.contains("range = 1..PropertySize.MAX_BATHROOMS"))
        assertTrue(
            "the minus no longer stops at the range's floor",
            services.contains("CounterStep(Icons.Outlined.Remove, outwardStart = true, enabled = value > range.first)"),
        )
    }
}
