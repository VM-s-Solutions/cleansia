package cz.cleansia.customer.features.profile

import java.io.File
import java.time.LocalDate
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The onboarding has no Compose harness here, so what a JVM test cannot render is pinned as source: the one checklist
 * behind Save, the mascot, the header and the example; one TalkBack read per row; and birth date wheels that store
 * the day as a wheel moves, where Done only closes and a sheet closed untouched stores nothing.
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

    @Test
    fun `Save, the mascot, the header and the example all read the one checklist`() {
        listOf(
            "OnboardingChecklist.of(",
            "enabled = ready && !saving",
            "if (ready) R.drawable.mascot_thumbs_up else R.drawable.mascot_waving",
            "checklist.doneCount",
            "reachable = checklist.phone",
        ).forEach { assertTrue("the screen no longer reads `$it`", source.contains(it)) }
        assertFalse("Save is gated on a phone that is merely not blank", source.contains("phone.isNotBlank()"))
    }

    /** Each row is one read, its state last, as the partner's onboarding steps read. */
    @Test
    fun `TalkBack reads each row once, ending with its state`() {
        assertTrue(
            "a line row no longer reads its label, value, note and state as one",
            source.contains("semantics(mergeDescendants = true) { contentDescription = listOfNotNull(label, value, note, state).joinToString(\", \") }"),
        )
        assertTrue(
            "the open row's head no longer reads its label and state as one",
            source.contains("semantics(mergeDescendants = true) { contentDescription = \"\$label, \$state\" }"),
        )
        listOf("onboarding_row_done_a11y", "onboarding_row_current_a11y", "onboarding_row_waiting_a11y").forEach {
            assertTrue("the rows no longer read $it", source.contains("R.string.$it"))
        }
        assertFalse("a state on TalkBack's own setting may be read first or not at all", source.contains("stateDescription"))
        assertFalse("the address is read a second time", source.contains("clearAndSetSemantics { contentDescription = email }"))
        assertTrue(
            "\"Not set\" is read inside a row",
            Regex("""stringResource\(R\.string\.onboarding_not_set\)[^\n]*clearAndSetSemantics \{\}""").containsMatchIn(source),
        )
    }

    @Test
    fun `the birth date note promises nothing the app does not give, in every locale`() {
        val note = mapOf(
            "values" to "Optional. Never shown to your cleaner.",
            "values-cs" to "Nepovinné. Uklízeči se nikdy nezobrazí.",
            "values-sk" to "Nepovinné. Upratovačovi sa nikdy nezobrazí.",
            "values-uk" to "Необов’язково. Клінер її ніколи не побачить.",
            "values-ru" to "Необязательно. Клинер её никогда не увидит.",
        )
        val perks = listOf("birthday perks", "narozeninové výhody", "narodeninové výhody", "бонусів до дня народження", "бонусов ко дню рождения")
        note.forEach { (dir, text) ->
            val xml = File(moduleDir, "src/main/res/$dir/strings.xml").readText()
            assertTrue("$dir: the birth date note is not \"$text\"", xml.contains("<string name=\"onboarding_birthdate_helper\">$text</string>"))
            perks.forEach { assertFalse("$dir still promises $it", xml.contains(it, ignoreCase = true)) }
        }
    }

    private val moduleDir: File by lazy {
        sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
            .first { File(it, "src/main/res/values/strings.xml").isFile }
    }

    private val source: String by lazy {
        File(moduleDir, "src/main/java/cz/cleansia/customer/features/profile/ProfileOnboardingScreen.kt")
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
