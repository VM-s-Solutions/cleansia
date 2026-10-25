package cz.cleansia.core.ui.components

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The Paste button under the boxes fills them with the clipboard's code, replacing what was typed. A fragment on
 * the clipboard must never complete a code from the digits typed before it: the e-mail code screens submit a code
 * as soon as it has six digits, and a wrong one spends one of its five attempts. The field's own long-press Paste
 * is ordinary editing and keeps [acceptOneTimeCode]'s insertion rule ([CodeInputPasteTest]).
 *
 * iOS's twin (`CodeInputPasteTests`) is held to the same cases; keep the two lists in step.
 */
class CodeInputPasteButtonTest {

    private fun press(clipboard: String, code: String) = pasteOneTimeCode(clipboard, code, 6)

    @Test
    fun `a whole code on the clipboard fills the empty boxes`() {
        assertEquals("123456", press("123456", code = ""))
    }

    @Test
    fun `a fragment on the clipboard replaces the typed digits and stays short of a code`() {
        assertEquals("3456", press("3456", code = "12"))
    }

    @Test
    fun `a code in a sentence replaces the typed digits`() {
        assertEquals("654321", press("Your code is 654321.", code = "12"))
    }

    @Test
    fun `a clipboard without digits leaves the typed digits as they are`() {
        assertEquals("12", press("abc", code = "12"))
        assertEquals("", press("", code = ""))
    }

    @Test
    fun `a run longer than a code fills the boxes with its first six digits`() {
        assertEquals("123456", press("1234567", code = "123"))
    }
}
