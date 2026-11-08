package cz.cleansia.customer.features.profile

import java.io.File
import java.time.LocalDate
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The onboarding has no Compose harness here, so what a JVM test cannot render is pinned as source. The birth date
 * wheels store the day as a wheel moves, Done only closes, and a sheet closed untouched stores nothing.
 */
class ProfileOnboardingScreenTest {

    private val today = LocalDate.of(2026, 11, 8)

    @Test
    fun `an empty birth date opens the wheels thirty years back`() {
        assertEquals(LocalDate.of(1996, 11, 8), birthDateWheelSeed("", today))
    }

    @Test
    fun `a stored birth date opens the wheels on that day`() {
        assertEquals(LocalDate.of(1991, 3, 14), birthDateWheelSeed("1991-03-14", today))
    }

    @Test
    fun `a stored value that is not a day opens the wheels thirty years back`() {
        assertEquals(LocalDate.of(1996, 11, 8), birthDateWheelSeed("14.03.1991", today))
    }

    @Test
    fun `only a moving wheel stores the day, and Done only closes`() {
        val sheet = body(source, "private fun BirthDateWheelSheet(")
        val listener = block(sheet, sheet.indexOf("init(seed.year, seed.monthValue - 1, seed.dayOfMonth)"))
        assertTrue("the wheels no longer store the day they show", listener.contains("latest(LocalDate.of(y, m + 1, d).toString())"))
        assertEquals(
            "the day is stored somewhere other than the wheels' listener",
            1,
            Regex("""\b(latest|onBirthDate)\(""").findAll(sheet).count(),
        )
        val done = block(sheet, sheet.indexOf("CleansiaTextButton(onClick ="))
        listOf("latest", "onBirthDate", "seed", "LocalDate").forEach {
            assertFalse("Done names a day ($it): $done", done.contains(it))
        }
        assertFalse("the sheet has a Cancel", sheet.contains("common_cancel"))
    }

    private val source: String by lazy {
        sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
            .map { File(it, "src/main/java/cz/cleansia/customer/features/profile/ProfileOnboardingScreen.kt") }
            .first { it.isFile }
            .readText()
            .replace("\r\n", "\n")
    }

    private fun body(text: String, declaration: String): String {
        val start = text.indexOf(declaration)
        assertTrue("$declaration not found", start >= 0)
        return block(text, text.indexOf(") {", start))
    }

    /** The text inside the first brace pair that opens at or after [from]. */
    private fun block(text: String, from: Int): String {
        assertTrue("block start not found", from >= 0)
        val open = text.indexOf('{', from)
        var depth = 1
        var i = open + 1
        while (i < text.length && depth > 0) {
            when (text[i]) {
                '{' -> depth++
                '}' -> depth--
            }
            i++
        }
        return text.substring(open + 1, i - 1)
    }
}
