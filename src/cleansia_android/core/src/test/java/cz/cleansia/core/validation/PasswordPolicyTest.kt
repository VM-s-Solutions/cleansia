package cz.cleansia.core.validation

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Pins [PasswordPolicy] to the server's rule so the two cannot drift apart
 * silently. The boundary is asserted explicitly in both directions: eight
 * characters is enough, seven is not.
 */
class PasswordPolicyTest {

    @Test
    fun `minimum length is the server's eight`() {
        assertEquals(8, PasswordPolicy.MIN_LENGTH)
    }

    @Test
    fun `eight characters with a letter and a digit is valid`() {
        assertTrue(PasswordPolicy.hasMinLength("Passw0rd"))
        assertTrue(PasswordPolicy.isValid("Passw0rd"))
    }

    @Test
    fun `seven characters is too short`() {
        assertFalse(PasswordPolicy.hasMinLength("Passw0r"))
        assertFalse(PasswordPolicy.isValid("Passw0r"))
    }

    @Test
    fun `a password with no digit is invalid`() {
        assertTrue(PasswordPolicy.hasLetter("Passwords"))
        assertFalse(PasswordPolicy.hasNumber("Passwords"))
        assertFalse(PasswordPolicy.isValid("Passwords"))
    }

    @Test
    fun `a password with no letter is invalid`() {
        assertTrue(PasswordPolicy.hasNumber("12345678"))
        assertFalse(PasswordPolicy.hasLetter("12345678"))
        assertFalse(PasswordPolicy.isValid("12345678"))
    }

    @Test
    fun `the change only ever loosens - a twelve character password stays valid`() {
        assertTrue(PasswordPolicy.isValid("Password1234"))
    }

    @Test
    fun `an empty password is invalid`() {
        assertFalse(PasswordPolicy.hasMinLength(""))
        assertFalse(PasswordPolicy.isValid(""))
    }

    @Test
    fun `passwordsMatch requires an identical non-empty confirmation`() {
        assertTrue(PasswordPolicy.passwordsMatch("Passw0rd", "Passw0rd"))
        assertFalse(PasswordPolicy.passwordsMatch("Passw0rd", "passw0rd"))
        assertFalse(PasswordPolicy.passwordsMatch("Passw0rd", "Passw0rdX"))
        assertFalse(PasswordPolicy.passwordsMatch("Passw0rd", ""))
        assertFalse(PasswordPolicy.passwordsMatch("", ""))
    }
}
