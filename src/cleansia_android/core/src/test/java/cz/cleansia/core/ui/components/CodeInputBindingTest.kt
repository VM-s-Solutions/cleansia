package cz.cleansia.core.ui.components

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
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

    /** The boxes are cleared from the semantics tree, so the field is all TalkBack reads; without a label it says "Edit box". */
    @Test
    fun `TalkBack names the field as the six-digit verification code`() {
        assertTrue(source.contains("val fieldLabel = stringResource(R.string.core_code_field_label)"))
        assertTrue(
            "the text field itself must carry the label",
            Regex("""\.focusRequester\(focusRequester\)\s*\.semantics\s*\{\s*contentDescription = fieldLabel\s*\}""")
                .containsMatchIn(source),
        )
    }

    @Test
    fun `the invisible caret is held at the end of the code`() {
        assertTrue(source.contains("TextFieldValue(code, selection = TextRange(code.length))"))
    }

    /** [CodeInputPasteButtonTest] proves the button's rule; this pins that the button is what uses it. */
    @Test
    fun `the paste button replaces the code with the one on the clipboard`() {
        assertTrue(
            "the button must paste the clipboard through pasteOneTimeCode, onto the code on screen",
            source.contains("pasteOneTimeCode(clipboard.getText()?.text.orEmpty(), code, length)"),
        )
        assertTrue(
            "a clipboard with no digits on it must leave a partial code alone",
            source.contains("if (pasted != code) onCodeChange(pasted)"),
        )
    }

    /**
     * getText() on Android 12+ tells the user the app read their clipboard; hasText() reads only its description.
     * Android 10+ answers neither until the app holds window focus, which it regains only after ON_RESUME.
     */
    @Test
    fun `the paste button is offered off the clip description and re-checked when the window regains focus`() {
        assertTrue(source.contains("val windowInfo = LocalWindowInfo.current"))
        assertTrue(
            Regex("""snapshotFlow\s*\{\s*windowInfo\.isWindowFocused\s*\}\s*\.collect\s*\{\s*focused\s*->\s*if \(focused\) clipboardHasText = clipboard\.hasText\(\)\s*\}""")
                .containsMatchIn(source),
        )
        assertFalse(
            "a lifecycle event comes before the window has focus back, so the check must not hang off one",
            source.contains("androidx.lifecycle"),
        )
        assertTrue(source.contains("if (clipboardHasText) {"))
    }
}
