package cz.cleansia.core.ui.components

import cz.cleansia.core.ui.components.SegmentEdit.Changed
import cz.cleansia.core.ui.components.SegmentEdit.Kept
import cz.cleansia.core.ui.components.SegmentEdit.Spread
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test

/**
 * A whole account pasted into one segment used to have its bank code merged into the number:
 * "12321414/3545" became the number "1232141435" and an empty bank code.
 *
 * iOS's twin (`CleansiaBankAccountFieldPasteTests`) and the web's (`cleansia-bank-account`) pin the same case
 * list; keep the three in step.
 */
class CleansiaBankAccountInputPasteTest {

    private val prefixBox = PrefixMaxLength
    private val numberBox = NumberMaxLength
    private val bankCodeBox = BankCodeMaxLength
    private val everyBox = listOf(prefixBox, numberBox, bankCodeBox)

    private fun edit(raw: String, into: Int, current: String = "") = editSegment(raw, current, into)

    /** A written-out account fills all three boxes, whichever one it was pasted into. */
    private fun assertSpreadFromEveryBox(expected: Spread, raw: String) =
        everyBox.forEach { box -> assertEquals("$raw into the $box-digit box", expected, edit(raw, box)) }

    /**
     * The case list the three platforms share. Each edit is the box's new text as the field reports it, the paste
     * after, before or over the text the box held. A null part of a [Spread] keeps what that box holds.
     */
    @Test
    fun `the case list the three platforms share`() {
        assertEquals("a bank code into an empty box", Changed("0800"), edit("0800", bankCodeBox))
        assertEquals("a bank code after one", Changed("5500"), edit("0800" + "5500", bankCodeBox, current = "0800"))
        assertEquals("a bank code before one", Changed("5500"), edit("5500" + "0800", bankCodeBox, current = "0800"))
        assertEquals("55 over the selected 08", Changed("5500"), edit("55" + "00", bankCodeBox, current = "0800"))
        assertEquals("a fragment completing the box", Changed("0800"), edit("08" + "00", bankCodeBox, current = "08"))
        assertEquals("a bank code after its own start", Changed("0800"), edit("08" + "0800", bankCodeBox, current = "08"))
        assertEquals(
            "a number after a bank code",
            Spread(null, "2000145399", null),
            edit("0800" + "2000145399", bankCodeBox, current = "0800"),
        )
        assertEquals("a number into an empty bank code", Spread(null, "2000145399", null), edit("2000145399", bankCodeBox))
        assertEquals("a prefix after a prefix", Changed("67"), edit("12345" + "67", prefixBox, current = "12345"))
        assertEquals("a prefix into an empty box", Changed("19"), edit("19", prefixBox))
        assertEquals(
            "a written-out account",
            Spread("19", "2000145399", "0800"),
            edit("19-2000145399/0800", numberBox),
        )
        assertEquals(
            "an IBAN",
            Spread("19", "2000145399", "0800"),
            edit("CZ65 0800 0000 1920 0014 5399", numberBox),
        )
        assertEquals("a digit typed into a full bank code", Kept, edit("0800" + "5", bankCodeBox, current = "0800"))
        assertEquals(
            "a number after a number",
            Changed("1234567890"),
            edit("2000145399" + "1234567890", numberBox, current = "2000145399"),
        )
    }

    @Test
    fun `a number and bank code split and clear the prefix`() {
        assertSpreadFromEveryBox(Spread("", "12321414", "3545"), "12321414/3545")
    }

    @Test
    fun `all three parts split`() {
        assertSpreadFromEveryBox(Spread("19", "2000145399", "0800"), "19-2000145399/0800")
    }

    /** Banking apps space the parts, and some use a no-break space or a narrow one. */
    @Test
    fun `whitespace of every kind is ignored`() {
        assertSpreadFromEveryBox(Spread("19", "2000145399", "0800"), " 19 - 2000145399 / 0800 ")
        assertSpreadFromEveryBox(Spread("19", "2000145399", "0800"), "19 - 2000145399 / 0800")
        assertSpreadFromEveryBox(Spread("", "12321414", "3545"), "12321414 /\n3545")
    }

    @Test
    fun `an en or em dash reads as a hyphen`() {
        assertSpreadFromEveryBox(Spread("19", "2000145399", "0800"), "19–2000145399/0800")
        assertSpreadFromEveryBox(Spread("19", "2000145399", "0800"), "19—2000145399/0800")
    }

    /** No bank code in the paste leaves the one already entered alone. */
    @Test
    fun `a prefix and number without a bank code keep the bank code`() {
        assertSpreadFromEveryBox(Spread("19", "2000145399", null), "19-2000145399")
    }

    /** A part copied on its own is pasted back into its own box, and the number already entered stays. */
    @Test
    fun `text that fits its box is kept as typed or pasted`() {
        assertEquals(Changed("000019"), edit("000019", prefixBox))
        assertEquals("pasted over all of the box's digits", Changed("5500"), edit("5500", bankCodeBox, current = "0800"))
        assertEquals("the box keeps digits only", Changed("19"), edit("1 9", prefixBox))
    }

    /**
     * A bare number too long for the box it landed in is the account number, so "2000145399" pasted
     * into the empty prefix box does not become the prefix "200014". The prefix and bank code already
     * entered stay.
     */
    @Test
    fun `a bare number too long for its box goes to the number and leaves the other two`() {
        assertEquals(Spread(null, "2000145399", null), edit("2000145399", prefixBox))
        assertEquals(Spread(null, "2000145399", null), edit("2000 1453 99", prefixBox))
        assertEquals(Spread(null, "1234567", null), edit("1234567", prefixBox))
        assertEquals(Spread(null, "12345", null), edit("12345", bankCodeBox))
        assertEquals(
            "pasted over a bank code",
            Spread(null, "2000145399", null),
            edit("2000145399", bankCodeBox, current = "0800"),
        )
    }

    @Test
    fun `a bare number pasted into the number box stays there`() {
        assertEquals(Changed("2000145399"), edit("2000145399", numberBox))
        assertEquals(Changed("19"), edit("19", numberBox))
    }

    /** The number pad types one digit at a time, and a typed prefix or bank code must stay where it is. */
    @Test
    fun `typing never jumps to the number`() {
        everyBox.forEach { box ->
            assertEquals(Changed("5"), edit("5", box))
            assertEquals(Changed(""), edit("", box))
            assertEquals(Changed(""), edit("", box, current = "19"))
        }
        assertEquals(Changed("12"), edit("12", prefixBox, current = "1"))
        assertEquals(Kept, edit("1234567", prefixBox, current = "123456"))
        assertEquals(Kept, edit("08001", bankCodeBox, current = "0800"))
        assertEquals(Kept, edit("20001453991", numberBox, current = "2000145399"))
    }

    /**
     * BasicTextField can report two edits before the screen recomposes, so the bank-code box is still
     * composed with "080" when "08001" arrives. Read against that, "08001" put in "01", which fits the box
     * and would replace its text. The second edit replaces "0800", the text the segment last reported, so
     * it is one digit typed into a full box, which keeps it.
     */
    @Test
    fun `two digits typed before a recomposition stay in their box`() {
        // As AccountSegment passes them on, with no recomposition between the two.
        val replaced = lastReported("080")
        assertEquals(Changed("0800"), edit("0800", bankCodeBox, current = replaced("0800")))
        assertEquals(Kept, edit("08001", bankCodeBox, current = replaced("08001")))
    }

    /** Read against the empty box it was composed with, "08001" would be five digits pasted, which go to the number. */
    @Test
    fun `a digit typed right after a bank code is pasted, before a recomposition, stays out of the number`() {
        val replaced = lastReported("")
        assertEquals(Changed("0800"), edit("0800", bankCodeBox, current = replaced("0800")))
        assertEquals(Kept, edit("08001", bankCodeBox, current = replaced("08001")))
    }

    @Test
    fun `a bare run too long for a number keeps the box's text`() {
        everyBox.forEach { box -> assertEquals("into the $box-digit box", Kept, edit("20001453991", box)) }
    }

    @Test
    fun `anything that is not an account is never spread`() {
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
        ).forEach { raw -> everyBox.forEach { box -> assertFalse("$raw into the $box-digit box", edit(raw, box) is Spread) } }
    }

    @Test
    fun `a Czech IBAN is broken into its domestic parts`() {
        assertSpreadFromEveryBox(Spread("19", "2000145399", "0800"), "CZ65 0800 0000 1920 0014 5399")
        assertSpreadFromEveryBox(Spread("19", "2000145399", "0800"), "cz6508000000192000145399")
    }

    @Test
    fun `a Slovak IBAN is broken into its domestic parts`() {
        assertSpreadFromEveryBox(Spread("19", "8742637541", "1200"), "SK31 1200 0000 1987 4263 7541")
    }

    /** Leading zeros are padding in the BBAN, not part of the written account; an all-zero prefix is none. */
    @Test
    fun `IBAN padding is dropped`() {
        assertSpreadFromEveryBox(Spread("", "123457", "0800"), "CZ55 0800 0000 0000 0012 3457")
    }

    @Test
    fun `other IBANs are not domestic accounts and keep the box's text`() {
        listOf(
            "DE89 3704 0044 0532 0130 00",
            "CZ65 0800 0000 1920 0014 539",
            "CZ65 0800 0000 1920 0014 53990",
            "CZ6X 0800 0000 1920 0014 5399",
            "CZ00 0800 0000 0000 0000 0000",
        ).forEach { raw -> everyBox.forEach { box -> assertEquals(raw, Kept, edit(raw, box)) } }
    }

    /** The server takes ASCII digits only; a keyboard can type Arabic-Indic or full-width ones. */
    @Test
    fun `a segment keeps ASCII digits only, up to its length`() {
        assertEquals("123", clampSegment("١٢٣123", 6))
        assertEquals("", clampSegment("１２３", 6))
        assertEquals("0800", clampSegment("08 00x99", 4))
    }
}
