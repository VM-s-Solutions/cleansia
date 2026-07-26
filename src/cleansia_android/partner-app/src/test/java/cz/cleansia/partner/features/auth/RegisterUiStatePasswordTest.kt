package cz.cleansia.partner.features.auth

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The register form must advertise exactly the policy the server enforces.
 *
 * Server truth is Cleansia.Core.AppServices/Common/Validators/ValidationExtensions.kt
 * `PasswordPattern = ^(?=.*[a-zA-Z])(?=.*\d).{8,}$` — a letter, a digit, eight
 * characters. The form used to demand twelve, so a password the API would have
 * accepted left the Register button disabled with no way to find out why.
 */
class RegisterUiStatePasswordTest {

    private fun state(password: String, confirm: String = password) =
        RegisterUiState(password = password, confirmPassword = confirm)

    @Test
    fun `an eight character password with a letter and a digit satisfies every rule`() {
        val s = state("Passw0rd")
        assertTrue(s.passwordHasMinLength)
        assertTrue(s.passwordHasLetter)
        assertTrue(s.passwordHasNumber)
        assertTrue(s.passwordsMatch)
    }

    @Test
    fun `seven characters is still too short`() {
        assertFalse(state("Passw0r").passwordHasMinLength)
    }

    @Test
    fun `letter and digit rules are unchanged`() {
        assertFalse(state("Passwords").passwordHasNumber)
        assertFalse(state("12345678").passwordHasLetter)
    }

    @Test
    fun `a twelve character password is still accepted`() {
        val s = state("Password1234")
        assertTrue(s.passwordHasMinLength)
        assertTrue(s.passwordHasLetter)
        assertTrue(s.passwordHasNumber)
    }

    @Test
    fun `confirmation must match and neither field may be blank`() {
        assertFalse(state("Passw0rd", "Passw0rdX").passwordsMatch)
        assertFalse(state("", "").passwordsMatch)
    }
}
