package cz.cleansia.core.ui.components

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * A whole account pasted into one segment used to have its bank code merged into the number:
 * "12321414/3545" became the number "1232141435" and an empty bank code. These pin the split. The iOS
 * twin's tests carry the same cases, because the two must read a paste identically.
 */
class CleansiaBankAccountInputPasteTest {

    private fun split(raw: String) = splitPastedAccount(raw)

    @Test
    fun `number and bank code without a prefix clear the prefix`() {
        assertEquals(Triple("", "12321414", "3545"), split("12321414/3545"))
    }

    @Test
    fun `prefix, number and bank code fill all three`() {
        assertEquals(Triple("19", "2000145399", "0800"), split("19-2000145399/0800"))
    }

    @Test
    fun `spaces of every kind and en or em dashes are read as the written form`() {
        val expected = Triple("19", "2000145399", "0800")
        listOf(
            " 19 - 2000145399 / 0800 ",
            "19 - 2000145399 / 0800",
            "19 - 2000145399/0800",
            "19–2000145399/0800",
            "19—2000145399/0800",
            "19-2000145399/0800\n",
        ).forEach { assertEquals(it, expected, split(it)) }
    }

    @Test
    fun `prefix and number without a bank code keep the bank code`() {
        assertEquals(Triple("19", "2000145399", null), split("19-2000145399"))
    }

    @Test
    fun `a bare number goes to the number and leaves prefix and bank code alone`() {
        assertEquals(Triple(null, "2000145399", null), split("2000145399"))
        assertEquals(Triple(null, "2000145399", null), split("2000 1453 99"))
    }

    @Test
    fun `a Czech IBAN is decomposed into prefix, number and bank code`() {
        assertEquals(Triple("19", "2000145399", "0800"), split("CZ65 0800 0000 1920 0014 5399"))
        assertEquals(Triple("19", "2000145399", "0800"), split("cz6508000000192000145399"))
    }

    @Test
    fun `a Czech IBAN without a prefix clears the prefix and drops the number's padding`() {
        assertEquals(Triple("", "123457", "0100"), split("CZ55 0100 0000 0000 0012 3457"))
    }

    @Test
    fun `a Slovak IBAN is decomposed the same way`() {
        assertEquals(Triple("19", "8742637541", "1200"), split("SK31 1200 0000 1987 4263 7541"))
    }

    @Test
    fun `typing one digit at a time never jumps segments`() {
        assertNull(splitPastedAccount("5", current = ""))
        assertNull(splitPastedAccount("12", current = "1"))
        assertNull(splitPastedAccount("200014539", current = "20001453"))
    }

    @Test
    fun `a paste over text already in the segment still counts when it adds two or more characters`() {
        assertEquals(Triple("", "12321414", "3545"), splitPastedAccount("12321414/3545", current = "12"))
        assertNull(splitPastedAccount("12321414/3545", current = "12321414/354"))
    }

    @Test
    fun `anything else is not an account`() {
        listOf(
            "1234567-1/0800",
            "12345678901/0800",
            "2000145399/08000",
            "20001/45/399",
            "19-",
            "/0800",
            "12345678901",
            "DE89 3704 0044 0532 0130 00",
            "CZ65 0800 0000 1920 0014 539",
            "abc",
            "",
        ).forEach { assertNull(it, split(it)) }
    }
}
