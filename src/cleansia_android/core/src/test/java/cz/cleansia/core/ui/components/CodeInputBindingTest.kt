package cz.cleansia.core.ui.components

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * [CodeInputPasteTest] proves the rule and cannot see whether the field uses it. The field used to be a 1 dp
 * text field beside the boxes, which nothing could tap or long-press, with the edit passed on as typed. These
 * pin the wiring that replaced it, scoped to the one file because the component has no test harness.
 */
class CodeInputBindingTest {

    private val source: String = sequenceOf(File("."), File("core"), File("src/cleansia_android/core"))
        .map { File(it, "src/main/java/cz/cleansia/core/ui/components/CodeInput.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("CodeInput.kt not found from ${File(".").absolutePath}")

    @Test
    fun `one text field is the whole control`() {
        assertEquals("text fields in CodeInput.kt", 1, Regex("""\bBasicTextField\(""").findAll(source).count())
        assertTrue(
            "the field must span the row so a tap anywhere on the boxes reaches it",
            Regex("""\.fillMaxWidth\(\)\s*\.focusRequester\(focusRequester\)""").containsMatchIn(source),
        )
        assertEquals(
            "the decoration must draw the inner field exactly once",
            1,
            Regex("""\binnerTextField\(\)""").findAll(source).count(),
        )
    }

    @Test
    fun `every edit goes through the one-time-code rule against the composed code`() {
        assertTrue(
            "the field must report acceptOneTimeCode(edit, code, length), not the edit as typed",
            source.contains("onCodeChange(acceptOneTimeCode(edit.text, code, length))"),
        )
    }

    @Test
    fun `the invisible caret is held at the end of the code`() {
        assertTrue(source.contains("TextFieldValue(code, selection = TextRange(code.length))"))
    }

    @Test
    fun `the paste button replaces the code with the one on the clipboard`() {
        assertTrue(
            "the button must read the clipboard through the same rule, with nothing typed to keep",
            source.contains("acceptOneTimeCode(clipboard.getText()?.text.orEmpty(), \"\", length)"),
        )
        assertTrue(
            "a clipboard with no digits on it must leave a partial code alone",
            source.contains("if (pasted.isNotEmpty()) onCodeChange(pasted)"),
        )
    }

    /** getText() on Android 12+ tells the user the app read their clipboard; hasText() reads only its description. */
    @Test
    fun `the paste button is offered off the clip description and re-checked on resume`() {
        assertTrue(
            Regex("""LifecycleEventEffect\(Lifecycle\.Event\.ON_RESUME\)\s*\{\s*clipboardHasText = clipboard\.hasText\(\)\s*\}""")
                .containsMatchIn(source),
        )
        assertTrue(source.contains("if (clipboardHasText) {"))
    }
}
