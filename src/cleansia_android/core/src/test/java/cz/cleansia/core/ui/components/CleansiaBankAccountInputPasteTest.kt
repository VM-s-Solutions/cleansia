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

    private fun split(raw: String) = splitPastedAccount(raw)?.toList()

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
     * A bare number is not split: it stays in the segment it was pasted into, where the digit clamp
     * takes it — in the number box, that is the number.
     */
    @Test
    fun `a bare number is not split`() {
        assertNull(split("2000145399"))
        assertNull(split("19"))
        assertNull(split(""))
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
}
