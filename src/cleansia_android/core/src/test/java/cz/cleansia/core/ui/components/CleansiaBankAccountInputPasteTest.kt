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

    private fun edit(raw: String, into: Int, current: String = "") = splitPastedAccount(raw, current, into)

    /** A written-out account fills all three boxes, whichever one it was pasted into. */
    private fun assertSpreadFromEveryBox(expected: Spread, raw: String) =
        everyBox.forEach { box -> assertEquals("$raw into the $box-digit box", expected, edit(raw, box)) }

    /**
     * The case list the three platforms share. Each edit is the box's new text as the field reports it, the paste or
     * the typed digit after, before or over the text the box held. A null part of a [Spread] keeps what that box holds.
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
        assertEquals("a digit typed after a full bank code", Changed("0800"), edit("0800" + "5", bankCodeBox, current = "0800"))
        assertEquals(
            "a number after a number",
            Changed("1234567890"),
            edit("2000145399" + "1234567890", numberBox, current = "2000145399"),
        )
        assertEquals("a short number and bank code", Spread("", "12345", "0800"), edit("12345/0800", numberBox))
        assertEquals("a short prefix and number", Spread("19", "1234567", null), edit("19-1234567", numberBox))
        assertEquals("a digit typed before a full bank code", Changed("5080"), edit("5" + "0800", bankCodeBox, current = "0800"))
        assertEquals(
            "an account over all of a bank code",
            Spread("19", "2000145399", "0800"),
            edit("19-2000145399/0800", bankCodeBox, current = "0800"),
        )
        assertEquals(
            "an account over all of a number",
            Spread("19", "2000145399", "0801"),
            edit("19-2000145399/0801", numberBox, current = "1234567890"),
        )
        assertEquals(
            "an account after a bank code",
            Spread("19", "2000145399", "0800"),
            edit("0800" + "19-2000145399/0800", bankCodeBox, current = "0800"),
        )
        assertEquals(
            "a number and bank code over all of the same number",
            Spread("", "2000145399", "0800"),
            edit("2000145399/0800", numberBox, current = "2000145399"),
        )
        assertEquals(
            "an account before the digits it starts with",
            Spread("19", "2000145399", "0800"),
            edit("19-2000145399/0800" + "19", numberBox, current = "19"),
        )
    }

    /**
     * Wrong, and accepted as rare: the paste starts with the "1" the selected "12345" starts with, and only the new
     * text arrives here, so that "1" reads as text the paste left in place. The prefix should be "19".
     */
    @Test
    fun `the known residual, an account pasted over part of a box's text, is split without a digit they share`() {
        assertEquals(
            "19-2000145399/0800 pasted over the 12345 of 1234567890",
            Spread("9", "2000145399", "0800"),
            edit("19-2000145399/0800" + "67890", numberBox, current = "1234567890"),
        )
    }

    /**
     * Wrong the same way: a digit the paste shares with the box's text at the same end reads as text the box kept.
     * "0100" should replace "0800", and the bank code should be "0800".
     */
    @Test
    fun `a known residual too, a paste that shares its first or last digits with the box's text can lose them`() {
        assertEquals("0100 pasted before 0800", Changed("1000"), edit("0100" + "0800", bankCodeBox, current = "0800"))
        assertEquals(
            "19-2000145399/0800 pasted over all of a bank code 0",
            Spread("19", "2000145399", "080"),
            edit("19-2000145399/0800", bankCodeBox, current = "0"),
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
        assertSpreadFromEveryBox(Spread("19", "2000145399", "0800"), "19\u00A0-\u00A02000145399\u202F/\u202F0800")
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

    /**
     * Typing puts in one character at a time, and a typed prefix or bank code must stay where it is; a "-" or "/"
     * typed among a box's digits is dropped like any other non-digit.
     */
    @Test
    fun `typing never jumps to the number`() {
        everyBox.forEach { box ->
            assertEquals(Changed("5"), edit("5", box))
            assertEquals(Changed(""), edit("", box))
            assertEquals(Changed(""), edit("", box, current = "19"))
        }
        assertEquals(Changed("12"), edit("12", prefixBox, current = "1"))
        assertEquals(Changed("123456"), edit("1234567", prefixBox, current = "123456"))
        assertEquals(Changed("0800"), edit("08001", bankCodeBox, current = "0800"))
        assertEquals(Changed("2000145399"), edit("20001453991", numberBox, current = "2000145399"))
        assertEquals("inside a full box", Changed("0850"), edit("08" + "5" + "00", bankCodeBox, current = "0800"))
        assertEquals("a hyphen inside a prefix", Changed("12345"), edit("1" + "-" + "2345", prefixBox, current = "12345"))
        assertEquals(
            "a slash inside a number",
            Changed("2000145399"),
            edit("200014" + "/" + "5399", numberBox, current = "2000145399"),
        )
    }

    /**
     * BasicTextField can report two edits before the screen recomposes, so the bank-code box is still
     * composed with "080" when "08001" arrives. Read against that, "08001" put in "01", which fits the box
     * and would replace its text. The second edit replaces "0800", the text the segment last reported, so
     * it is one digit typed at the end of a full box, which the box's clamp drops.
     */
    @Test
    fun `two digits typed before a recomposition stay in their box`() {
        // As AccountSegment passes them on, with no recomposition between the two.
        val replaced = lastReported("080")
        assertEquals(Changed("0800"), edit("0800", bankCodeBox, current = replaced("0800")))
        assertEquals(Changed("0800"), edit("08001", bankCodeBox, current = replaced("08001")))
    }

    /** Read against the empty box it was composed with, "08001" would be five digits pasted, which go to the number. */
    @Test
    fun `a digit typed right after a bank code is pasted, before a recomposition, stays out of the number`() {
        val replaced = lastReported("")
        assertEquals(Changed("0800"), edit("0800", bankCodeBox, current = replaced("0800")))
        assertEquals(Changed("0800"), edit("08001", bankCodeBox, current = replaced("08001")))
    }

    /**
     * A box that kept an edit out shows its own text again at the next composition, where the segment builds a new
     * reporter ([CleansiaBankAccountInputBindingTest]). Kept across it, the reporter would answer the "08005" kept
     * out, and "5500" pasted after "0800" would put in "500".
     */
    @Test
    fun `a paste after a digit the full box kept out replaces the box's text`() {
        val typing = lastReported("0800")
        assertEquals(Changed("0800"), edit("08005", bankCodeBox, current = typing("08005")))

        val atTheNextComposition = lastReported("0800")
        assertEquals(Changed("5500"), edit("08005500", bankCodeBox, current = atTheNextComposition("08005500")))
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
