package cz.cleansia.customer.features.auth

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The confirmation sets the account's password, so when the confirm screen asks for one the Verify button
 * waits for a password that keeps the server's rules, typed twice the same.
 */
class EmailVerifyFormTest {

    private fun submittable(
        code: String = "123456",
        asksForPassword: Boolean = true,
        password: String = "Heslo2026",
        confirmPassword: String = password,
    ) = EmailVerifyForm.isSubmittable(code, asksForPassword, password, confirmPassword)

    @Test
    fun `the code alone submits when no password is asked`() {
        assertTrue(submittable(asksForPassword = false, password = "", confirmPassword = ""))
    }

    @Test
    fun `an incomplete code never submits`() {
        assertFalse(submittable(code = "12345", asksForPassword = false))
        assertFalse(submittable(code = "12345"))
    }

    @Test
    fun `an asked password that keeps the rules and matches submits`() {
        assertTrue(submittable())
    }

    @Test
    fun `an asked password that breaks a rule, does not match or is missing does not`() {
        assertFalse(submittable(password = "short1"))
        assertFalse(submittable(password = "noDigitsHere"))
        assertFalse(submittable(password = "Heslo2026", confirmPassword = "Heslo2027"))
        assertFalse(submittable(password = ""))
    }
}
