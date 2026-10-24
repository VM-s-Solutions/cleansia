package cz.cleansia.core.ui.components

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * A code pasted, or dropped in by the keyboard's clipboard chip, after two typed digits used to be appended to
 * them: "12" plus "654321" became "126543", which the screens auto-submit and which spends one of the code's
 * five attempts.
 *
 * iOS's twin (`OneTimeCodeTests`) is held to the same cases; keep the two lists in step.
 */
class CodeInputPasteTest {

    private fun accept(raw: String, current: String = "") = acceptOneTimeCode(raw, current, 6)

    @Test
    fun `a whole code pasted into the empty boxes fills them`() {
        assertEquals("123456", accept("123456"))
    }

    @Test
    fun `the code is read out of the text around it`() {
        assertEquals("123456", accept("Your code: 123 456"))
        assertEquals("123456", accept("Your code is 123456."))
    }

    @Test
    fun `a whole code inserted after typed digits replaces them`() {
        assertEquals("654321", accept("12654321", current = "12"))
    }

    /** The long-press toolbar pastes over the word it selected, which here is every digit typed so far. */
    @Test
    fun `a whole code pasted over a selection replaces it`() {
        assertEquals("654321", accept("654321", current = "12"))
    }

    @Test
    fun `a whole code inserted into a full one replaces it`() {
        assertEquals("123456", accept("123456123456", current = "123456"))
        assertEquals("654321", accept("123456Your code is 654321", current = "123456"))
    }

    @Test
    fun `typing grows the code one digit at a time and backspace takes one off`() {
        assertEquals("1", accept("1"))
        assertEquals("12", accept("12", current = "1"))
        assertEquals("123", accept("123", current = "12"))
        assertEquals("12", accept("12", current = "123"))
        assertEquals("", accept("", current = "1"))
    }

    /**
     * BasicTextField can report two edits before the screen recomposes, so the second one arrives against the
     * code the field was composed with. Two digits at once are still typing: only a whole code replaces.
     */
    @Test
    fun `digits typed faster than a recomposition are still typing`() {
        assertEquals("12", accept("12"))
        assertEquals("12345", accept("12345", current = "12"))
    }

    @Test
    fun `a seventh typed digit is ignored`() {
        assertEquals("123456", accept("1234567", current = "123456"))
    }

    /** The server takes ASCII digits only; a keyboard can type Arabic-Indic or full-width ones. */
    @Test
    fun `digits other than ASCII are dropped`() {
        assertEquals("", accept("١٢٣٤٥٦"))
        assertEquals("", accept("１２３４５６"))
        assertEquals("12", accept("12٣", current = "12"))
        assertEquals("123456", accept("١٢٣123456"))
    }

    @Test
    fun `a paste with no digits leaves the code unchanged`() {
        assertEquals("12", accept("12abc", current = "12"))
        assertEquals("", accept("hello"))
    }

    /** By design: a fragment shorter than a code is an edit like any other, so it is added and clamped. */
    @Test
    fun `a fragment shorter than a code is added to it`() {
        assertEquals("1234", accept("1234", current = "12"))
        assertEquals("123456", accept("1234567", current = "12"))
    }
}
