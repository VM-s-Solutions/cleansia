package cz.cleansia.customer.features.profile

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * The notifications screen used to carry two toggles — "Email" and "SMS" — whose
 * state lived in a composable-local `remember { mutableStateOf(...) }`. Nothing
 * ever read them: they were not persisted, not sent, and reset to their defaults
 * every time the screen was entered. A user could switch SMS on, leave, come
 * back, and find it off again, having been told nothing. They were removed
 * because the backend models push categories only.
 *
 * The screen has no automated coverage of its own — it is one big `@Composable`
 * with no seam — so these tests work on the source and resource files directly.
 * That is deliberately literal, but it pins the two things a compiler cannot:
 * that no toggle on this screen holds state the backend never sees, and that a
 * deletion spanning five locale files was carried out in all five.
 */
class NotificationsScreenTogglesTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /** Copy that existed only to label the two fake toggles. */
    private val removedKeys = listOf(
        "notifications_section_channels",
        "notifications_email",
        "notifications_email_desc",
        "notifications_sms",
        "notifications_sms_desc",
    )

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res/values/strings.xml").isFile }
        ?: error("customer-app module not found from working dir ${File(".").absolutePath}")

    private fun stringsXml(locale: String): String {
        val file = File(moduleDir, "src/main/res/$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    private val screenSource: String = File(
        moduleDir,
        "src/main/java/cz/cleansia/customer/features/profile/NotificationsScreen.kt",
    ).also { assertTrue("NotificationsScreen.kt not found at ${it.absolutePath}", it.isFile) }
        .readText()

    /**
     * The defect itself: a switch the user can flip that changes nothing outside
     * the composition. Every toggle on this screen must hand its new value to the
     * ViewModel, which is what actually persists it. `ToggleRow`'s own parameter
     * forwarding (`onCheckedChange = onCheckedChange`) is not a lambda literal and
     * so is not matched here.
     */
    @Test
    fun `every toggle on the screen writes through to the ViewModel`() {
        val handlers = screenSource.lines()
            .map { it.trim() }
            .filter { it.startsWith("onCheckedChange = {") }

        assertTrue("expected the screen to still render toggles", handlers.isNotEmpty())

        val localOnly = handlers.filterNot { it.contains("viewModel.setCategory") }
        if (localOnly.isNotEmpty()) {
            fail(
                "these toggles do not reach the ViewModel, so flipping them changes " +
                    "nothing the user can observe after leaving the screen: " +
                    localOnly.joinToString(" | "),
            )
        }
    }

    /**
     * Android falls back to `values/` for any row a locale is missing and this
     * module has no lint step in CI, so a half-finished deletion is invisible at
     * build time and leaves dead copy rotting in four files.
     */
    @Test
    fun `the fake-channel strings are gone from all five locales`() {
        val leftovers = mutableListOf<String>()
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            removedKeys.forEach { key ->
                if (xml.contains("name=\"$key\"")) leftovers += "$locale/$key"
            }
        }
        if (leftovers.isNotEmpty()) {
            fail("orphaned string rows with no code reference: ${leftovers.joinToString()}")
        }
    }

    /**
     * The general form of the same hazard: the five files must always declare the
     * same key set, whichever direction a change moves in.
     */
    @Test
    fun `all five locales declare the same keys`() {
        val keyPattern = Regex("<string name=\"([^\"]+)\"")
        val english = keyPattern.findAll(stringsXml("values")).map { it.groupValues[1] }.toSet()
        locales.drop(1).forEach { locale ->
            val translated = keyPattern.findAll(stringsXml(locale)).map { it.groupValues[1] }.toSet()
            assertEquals(
                "$locale is missing rows: ${(english - translated).sorted()}; " +
                    "and declares rows values/ does not: ${(translated - english).sorted()}",
                english,
                translated,
            )
        }
    }
}
