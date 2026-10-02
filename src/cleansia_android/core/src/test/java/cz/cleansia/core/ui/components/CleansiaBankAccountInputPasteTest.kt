package cz.cleansia.core.ui.components

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * A whole account pasted into one segment used to have its bank code merged into the number:
 * "12321414/3545" became the number "1232141435" and an empty bank code.
 *
 * iOS's twin (`CleansiaBankAccountFieldPasteTests`) is held to the same cases; keep the two lists in
 * step.
 */
class CleansiaBankAccountInputPasteTest {

    private fun split(raw: String, current: String = "") = splitPastedAccount(raw, current)?.toList()

    @Test
    fun `a number and bank code split and clear the prefix`() {
        assertEquals(listOf("", "12321414", "3545"), split("12321414/3545"))
    }

    @Test
    fun `all three parts split`() {
        assertEquals(listOf("19", "2000145399", "0800"), split("19-2000145399/0800"))
    }

    /** Banking apps space the parts, and some use a no-break space or a narrow one. */
    @Test
    fun `whitespace of every kind is ignored`() {
        assertEquals(listOf("19", "2000145399", "0800"), split(" 19 - 2000145399 / 0800 "))
        assertEquals(listOf("19", "2000145399", "0800"), split("19 - 2000145399 / 0800"))
        assertEquals(listOf("", "12321414", "3545"), split("12321414 /\n3545"))
    }

    @Test
    fun `an en or em dash reads as a hyphen`() {
        assertEquals(listOf("19", "2000145399", "0800"), split("19–2000145399/0800"))
        assertEquals(listOf("19", "2000145399", "0800"), split("19—2000145399/0800"))
    }

    /** No bank code in the paste leaves the one already entered alone. */
    @Test
    fun `a prefix and number without a bank code keep the bank code`() {
        assertEquals(listOf("19", "2000145399", null), split("19-2000145399"))
    }

    /**
     * A bare number is the account number whichever box received it, so "2000145399" pasted into the
     * empty prefix box no longer becomes the prefix "200014". The split never learns which box it was:
     * these are the prefix, number and bank-code boxes alike, and prefix and bank code are left alone.
     */
    @Test
    fun `a bare pasted number goes to the number and leaves the other two`() {
        assertEquals(listOf(null, "2000145399", null), split("2000145399"))
        assertEquals(listOf(null, "2000145399", null), split("2000 1453 99"))
        assertEquals(
            "pasted over a bank code",
            listOf(null, "2000145399", null),
            split("2000145399", current = "0800"),
        )
        // Owner decision D14 as written: two digits pasted into the empty prefix box are the number too.
        assertEquals(listOf(null, "19", null), split("19"))
    }

    /** The number pad types one digit at a time, and a typed prefix or bank code must stay where it is. */
    @Test
    fun `typing never jumps to the number`() {
        assertNull(split("5"))
        assertNull(split("12", current = "1"))
        assertNull(split("08001", current = "0800"))
        assertNull(split(""))
        assertNull(split("", current = "19"))
    }

    /**
     * BasicTextField can report two edits before the screen recomposes, so the segment is still composed
     * with "" when "12" arrives. Read against that, "12" was two characters at once — a bare paste — and
     * overwrote the number while the prefix stayed "1". The second edit replaces "1", the text the
     * segment last reported.
     */
    @Test
    fun `two digits typed before a recomposition stay in their box`() {
        // As AccountSegment passes them on, with no recomposition between the two.
        val replaced = lastReported("")
        assertNull(split("1", current = replaced("1")))
        assertNull(split("12", current = replaced("12")))
    }

    @Test
    fun `a bare run too long for a number is left to the clamp`() {
        assertNull(split("20001453991"))
        assertNull("pasted after a bank code already there", split("08002000145399", current = "0800"))
    }

    @Test
    fun `anything that is not an account is left to the clamp`() {
        listOf(
            "1234567-1/0800",
            "12345678901/0800",
            "2000145399/08000",
            "20001/45/399",
            "19-20-2000145399/0800",
            "-2000145399/0800",
            "19-/0800",
            "2000145399/",
            "/0800",
            "12a4/0800",
            "١٢٣/0800",
        ).forEach { assertNull(it, split(it)) }
    }

    @Test
    fun `a Czech IBAN is broken into its domestic parts`() {
        assertEquals(listOf("19", "2000145399", "0800"), split("CZ65 0800 0000 1920 0014 5399"))
        assertEquals(listOf("19", "2000145399", "0800"), split("cz6508000000192000145399"))
    }

    @Test
    fun `a Slovak IBAN is broken into its domestic parts`() {
        assertEquals(listOf("19", "8742637541", "1200"), split("SK31 1200 0000 1987 4263 7541"))
    }

    /** Leading zeros are padding in the BBAN, not part of the written account; an all-zero prefix is none. */
    @Test
    fun `IBAN padding is dropped`() {
        assertEquals(listOf("", "123457", "0800"), split("CZ55 0800 0000 0000 0012 3457"))
    }

    @Test
    fun `other IBANs are not domestic accounts`() {
        listOf(
            "DE89 3704 0044 0532 0130 00",
            "CZ65 0800 0000 1920 0014 539",
            "CZ65 0800 0000 1920 0014 53990",
            "CZ6X 0800 0000 1920 0014 5399",
            "CZ00 0800 0000 0000 0000 0000",
        ).forEach { assertNull(it, split(it)) }
    }

    /** The server takes ASCII digits only; a keyboard can type Arabic-Indic or full-width ones. */
    @Test
    fun `a segment keeps ASCII digits only, up to its length`() {
        assertEquals("123", clampSegment("١٢٣123", 6))
        assertEquals("", clampSegment("１２３", 6))
        assertEquals("0800", clampSegment("08 00x99", 4))
    }
}
