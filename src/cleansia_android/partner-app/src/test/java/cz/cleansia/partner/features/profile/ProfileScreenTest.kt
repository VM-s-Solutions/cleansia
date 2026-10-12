package cz.cleansia.partner.features.profile

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The Profile hub draws to the top edge and its hero pads the status bar itself, so its rows scroll under
 * the clock; like the dashboard, the hub wears the shared fade. This module has no Compose test harness,
 * so what the screen binds is read as source.
 */
class ProfileScreenTest {

    @Test
    fun `the Profile hub fades its list under the status bar`() {
        val screen = code()
        assertTrue("the hub must hold its list's state", screen.contains("val listState = rememberLazyListState()"))
        assertTrue("the hub must have exactly one list", screen.indexOf("LazyColumn(") == screen.lastIndexOf("LazyColumn("))
        val list = block(screen, "LazyColumn(", '(', ')')
        assertTrue("the LazyColumn must be driven by that state", list.contains("state = listState"))
        assertTrue(
            "the LazyColumn must chain .statusBarFade(listState)",
            Regex("""\.statusBarFade\(listState\)""").containsMatchIn(list),
        )
    }

    private fun code(): String = source.replace(Regex("""/\*[\s\S]*?\*/|(?<![:"])//[^\n]*"""), "")

    /** The bracketed block that opens first after [marker]. */
    private fun block(text: String, marker: String, opener: Char, closer: Char): String {
        val start = text.indexOf(marker)
        assertTrue("no `$marker` in ProfileScreen.kt", start >= 0)
        val open = text.indexOf(opener, start)
        var depth = 0
        for (i in open until text.length) {
            when (text[i]) {
                opener -> depth++
                closer -> if (--depth == 0) return text.substring(open, i + 1)
            }
        }
        error("unbalanced `$opener` after `$marker`")
    }

    private val source: String = sequenceOf(
        File("."),
        File("partner-app"),
        File("src/cleansia_android/partner-app"),
    ).map { File(it, "src/main/java/cz/cleansia/partner/features/profile/ProfileScreen.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?.replace("\r\n", "\n")
        ?: error("ProfileScreen.kt not found from working dir ${File(".").absolutePath}")
}
