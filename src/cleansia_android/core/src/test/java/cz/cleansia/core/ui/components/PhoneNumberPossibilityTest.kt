package cz.cleansia.core.ui.components

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * What the device checks of a phone: international form, and a length libphonenumber holds possible for the
 * country. Whether the number is valid is the server's (`PhoneNumberAttribute`), whose metadata is newer than
 * the app's. The iOS twin, in `PhoneNumberKitDisplayFormatter`, takes the same table.
 */
class PhoneNumberPossibilityTest {

    @Test
    fun `an international number of a possible length passes`() {
        listOf("+420777123456", "+421912345678", "+420 777 123 456").forEach {
            assertTrue(it, isPossiblePhoneNumber(it))
        }
    }

    /** No Czech number starts with 1, but a range allocated after this library's metadata could look the same. */
    @Test
    fun `a number libphonenumber does not hold valid passes when its length is possible`() {
        assertTrue(isPossiblePhoneNumber("+420123456789"))
    }

    @Test
    fun `a number without its country code, a bare plus, or one too short or too long does not`() {
        listOf("777123456", "420777123456", "+", "+1", "+4201", "+42077712345", "+4207771234567890", "").forEach {
            assertFalse(it, isPossiblePhoneNumber(it))
        }
    }
}
