package cz.cleansia.core.ui.components

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * A pasted number used to lose its country code whenever anything came before the `+`: a space, a
 * "Tel:" label, a bracket, or the invisible direction marks macOS Contacts wraps a number in. The iOS
 * twin (`PhoneNumberSanitizerTests`) is held to the same cases.
 */
class PhoneNumberSanitizerTest {

    @Test
    fun `a plus before the first digit is kept wherever it sits`() {
        listOf(
            "+420 728 089 247",
            " +420 728 089 247",
            "‭+420 728 089 247‬",
            "Tel: +420 728 089 247",
            "(+420) 728 089 247",
            "+420-728-089-247",
        ).forEach { assertEquals(it, "+420728089247", sanitizePhoneInput(it)) }
    }

    @Test
    fun `a plus after a kept digit or a kept plus is dropped`() {
        assertEquals("4207", sanitizePhoneInput("4+20+7"))
        assertEquals("+420", sanitizePhoneInput("++420"))
    }

    @Test
    fun `a number without a plus keeps its digits only`() {
        assertEquals("420728089247", sanitizePhoneInput("(420) 728 089 247"))
        assertEquals("123456", sanitizePhoneInput("ab123cd456"))
        assertEquals("", sanitizePhoneInput(""))
    }
}
