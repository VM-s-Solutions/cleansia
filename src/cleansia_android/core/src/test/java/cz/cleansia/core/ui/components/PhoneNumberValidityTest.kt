package cz.cleansia.core.ui.components

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The server's own rule for a stored phone (`PhoneNumberAttribute`): a number in international form that
 * libphonenumber holds valid. The iOS twin (`PhoneNumberKitDisplayFormatter.isValidNumber`) takes the
 * same table.
 */
class PhoneNumberValidityTest {

    @Test
    fun `an international number libphonenumber holds valid passes`() {
        listOf("+420777123456", "+421912345678", "+420 777 123 456").forEach {
            assertTrue(it, isValidPhoneNumber(it))
        }
    }

    @Test
    fun `a number without its country code, a bare plus or a number too short does not`() {
        listOf("777123456", "420777123456", "+", "+1", "+4201", "+420123456789", "").forEach {
            assertFalse(it, isValidPhoneNumber(it))
        }
    }
}
