package cz.cleansia.core.ui.components

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * A whole account pasted into one segment used to have its bank code merged into the number:
 * "12321414/3545" became the number "1232141435" and an empty bank code.
 *
 * iOS's twin (`CleansiaBankAccountFieldPasteTests`) and the web's (`cleansia-bank-account`) are held to
 * the same cases; keep the lists in step.
 */
class CleansiaBankAccountInputPasteTest {

    private val prefixBox = PrefixMaxLength
    private val numberBox = NumberMaxLength
    private val bankCodeBox = BankCodeMaxLength
    private val everyBox = listOf(prefixBox, numberBox, bankCodeBox)

    private fun split(raw: String, into: Int, current: String = "") =
        splitPastedAccount(raw, current, into)?.toList()

    /** A written-out account fills all three boxes, whichever one it was pasted into. */
    private fun assertSplitsFromEveryBox(expected: List<String?>, raw: String) =
        everyBox.forEach { box -> assertEquals("$raw into the $box-digit box", expected, split(raw, box)) }

    @Test
    fun `a number and bank code split and clear the prefix`() {
        assertSplitsFromEveryBox(listOf("", "12321414", "3545"), "12321414/3545")
    }

    @Test
    fun `all three parts split`() {
        assertSplitsFromEveryBox(listOf("19", "2000145399", "0800"), "19-2000145399/0800")
    }

    /** Banking apps space the parts, and some use a no-break space or a narrow one. */
    @Test
    fun `whitespace of every kind is ignored`() {
        assertSplitsFromEveryBox(listOf("19", "2000145399", "0800"), " 19 - 2000145399 / 0800 ")
        assertSplitsFromEveryBox(listOf("19", "2000145399", "0800"), "19 - 2000145399 / 0800")
        assertSplitsFromEveryBox(listOf("", "12321414", "3545"), "12321414 /\n3545")
    }

    @Test
    fun `an en or em dash reads as a hyphen`() {
        assertSplitsFromEveryBox(listOf("19", "2000145399", "0800"), "19–2000145399/0800")
        assertSplitsFromEveryBox(listOf("19", "2000145399", "0800"), "19—2000145399/0800")
    }

    /** No bank code in the paste leaves the one already entered alone. */
    @Test
    fun `a prefix and number without a bank code keep the bank code`() {
        assertSplitsFromEveryBox(listOf("19", "2000145399", null), "19-2000145399")
    }

    /** A part copied on its own is pasted back into its own box, and the number already entered stays. */
    @Test
    fun `a bare bank code or prefix that fits its own box stays there`() {
        assertNull(split("0800", into = bankCodeBox))
        assertEquals("0800", clampSegment("0800", bankCodeBox))
        assertNull(split("19", into = prefixBox))
        assertNull(split("000019", into = prefixBox))
        assertNull("pasted over the box's own digits", split("5500", into = bankCodeBox, current = "0800"))
    }

    /**
     * A bare number too long for the box it landed in is the account number, so "2000145399" pasted
     * into the empty prefix box does not become the prefix "200014". The prefix and bank code already
     * entered stay.
     */
    @Test
    fun `a bare number too long for its box goes to the number and leaves the other two`() {
        assertEquals(listOf(null, "2000145399", null), split("2000145399", into = prefixBox))
        assertEquals(listOf(null, "2000145399", null), split("2000 1453 99", into = prefixBox))
        assertEquals(listOf(null, "1234567", null), split("1234567", into = prefixBox))
        assertEquals(listOf(null, "12345", null), split("12345", into = bankCodeBox))
        assertEquals(
            "pasted over a bank code",
            listOf(null, "2000145399", null),
            split("2000145399", into = bankCodeBox, current = "0800"),
        )
    }

    @Test
    fun `a bare number pasted into the number box stays there`() {
        assertNull(split("2000145399", into = numberBox))
        assertNull(split("19", into = numberBox))
    }

    /** The number pad types one digit at a time, and a typed prefix or bank code must stay where it is. */
    @Test
    fun `typing never jumps to the number`() {
        everyBox.forEach { box ->
            assertNull(split("5", into = box))
            assertNull(split("", into = box))
            assertNull(split("", into = box, current = "19"))
        }
        assertNull(split("12", into = prefixBox, current = "1"))
        assertNull(split("1234567", into = prefixBox, current = "123456"))
        assertNull(split("08001", into = bankCodeBox, current = "0800"))
    }

    /**
     * BasicTextField can report two edits before the screen recomposes, so the bank-code box is still
     * composed with "080" when "08001" arrives. Read against that, "08001" was two characters at once,
     * too long for the box — a bare paste of the number. The second edit replaces "0800", the text the
     * segment last reported.
     */
    @Test
    fun `two digits typed before a recomposition stay in their box`() {
        // As AccountSegment passes them on, with no recomposition between the two.
        val replaced = lastReported("080")
        assertNull(split("0800", into = bankCodeBox, current = replaced("0800")))
        assertNull(split("08001", into = bankCodeBox, current = replaced("08001")))
    }

    @Test
    fun `a bare run too long for a number is left to the clamp`() {
        everyBox.forEach { box -> assertNull(split("20001453991", into = box)) }
        assertNull("pasted after a bank code already there", split("08002000145399", into = bankCodeBox, current = "0800"))
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
        ).forEach { raw -> everyBox.forEach { box -> assertNull(raw, split(raw, into = box)) } }
    }

    @Test
    fun `a Czech IBAN is broken into its domestic parts`() {
        assertSplitsFromEveryBox(listOf("19", "2000145399", "0800"), "CZ65 0800 0000 1920 0014 5399")
        assertSplitsFromEveryBox(listOf("19", "2000145399", "0800"), "cz6508000000192000145399")
    }

    @Test
    fun `a Slovak IBAN is broken into its domestic parts`() {
        assertSplitsFromEveryBox(listOf("19", "8742637541", "1200"), "SK31 1200 0000 1987 4263 7541")
    }

    /** Leading zeros are padding in the BBAN, not part of the written account; an all-zero prefix is none. */
    @Test
    fun `IBAN padding is dropped`() {
        assertSplitsFromEveryBox(listOf("", "123457", "0800"), "CZ55 0800 0000 0000 0012 3457")
    }

    @Test
    fun `other IBANs are not domestic accounts`() {
        listOf(
            "DE89 3704 0044 0532 0130 00",
            "CZ65 0800 0000 1920 0014 539",
            "CZ65 0800 0000 1920 0014 53990",
            "CZ6X 0800 0000 1920 0014 5399",
            "CZ00 0800 0000 0000 0000 0000",
        ).forEach { raw -> everyBox.forEach { box -> assertNull(raw, split(raw, into = box)) } }
    }

    /** The server takes ASCII digits only; a keyboard can type Arabic-Indic or full-width ones. */
    @Test
    fun `a segment keeps ASCII digits only, up to its length`() {
        assertEquals("123", clampSegment("١٢٣123", 6))
        assertEquals("", clampSegment("１２３", 6))
        assertEquals("0800", clampSegment("08 00x99", 4))
    }
}
