package cz.cleansia.customer.features.auth

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The terms tick is a hard submit blocker, not a hint. Without this the "unticked box
 * records nothing" rule would be the only thing standing between an untickable form and a
 * silent legal gap — and that rule can only ever be reached if this one fails.
 */
class SignUpFormTest {

    private fun submittable(
        firstName: String = "Ada",
        lastName: String = "Lovelace",
        email: String = "ada@example.com",
        password: String = "Passw0rd",
        confirmPassword: String = "Passw0rd",
        acceptedTerms: Boolean = true,
    ) = SignUpForm.isSubmittable(
        firstName = firstName,
        lastName = lastName,
        email = email,
        password = password,
        confirmPassword = confirmPassword,
        acceptedTerms = acceptedTerms,
    )

    @Test
    fun anOtherwiseCompleteFormIsSubmittable() {
        assertTrue(submittable())
    }

    @Test
    fun anUntickedTermsBoxBlocksSubmit() {
        assertFalse(submittable(acceptedTerms = false))
    }

    @Test
    fun theOtherFieldsStillGate() {
        assertFalse(submittable(firstName = " "))
        assertFalse(submittable(lastName = ""))
        assertFalse(submittable(email = ""))
        assertFalse(submittable(password = "short1"))
        assertFalse(submittable(confirmPassword = "Different1"))
    }
}
