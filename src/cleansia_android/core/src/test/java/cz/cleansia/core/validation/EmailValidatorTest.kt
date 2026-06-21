package cz.cleansia.core.validation

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Pure-JVM tests for [EmailValidator] — the whole point of the extraction is that
 * email validation no longer needs an Android runtime to be unit-tested.
 */
class EmailValidatorTest {

    @Test
    fun `accepts ordinary addresses`() {
        assertTrue(EmailValidator.isValid("user@example.com"))
        assertTrue(EmailValidator.isValid("first.last@sub.example.co.uk"))
        assertTrue(EmailValidator.isValid("name+tag@example.io"))
        assertTrue(EmailValidator.isValid("a_b-c%d@example-domain.com"))
    }

    @Test
    fun `rejects malformed addresses`() {
        assertFalse(EmailValidator.isValid(""))
        assertFalse(EmailValidator.isValid("plainstring"))
        assertFalse(EmailValidator.isValid("missing-domain@"))
        assertFalse(EmailValidator.isValid("@missing-local.com"))
        assertFalse(EmailValidator.isValid("no-at-symbol.com"))
        assertFalse(EmailValidator.isValid("trailing-dot@example."))
        assertFalse(EmailValidator.isValid("spaces in@example.com"))
    }
}
