package cz.cleansia.customer.core.booking

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/** `BookingPolicy.AllowsCash` on the client: a signed-in customer, on a booking one cleaner does alone. */
class CashEligibilityTest {

    @Test
    fun `a signed-in customer on a one-cleaner booking may pay in cash`() {
        assertEquals(CashEligibility.Available, CashEligibility.resolve(signedIn = true, requiredEmployees = 1))
    }

    @Test
    fun `a guest on a one-cleaner booking is asked to sign in`() {
        assertEquals(CashEligibility.NeedsAccount, CashEligibility.resolve(signedIn = false, requiredEmployees = 1))
    }

    @Test
    fun `a signed-in customer on a two-cleaner booking pays by card`() {
        assertEquals(CashEligibility.NeedsCard(2), CashEligibility.resolve(signedIn = true, requiredEmployees = 2))
    }

    @Test
    fun `a guest on a two-cleaner booking is told about the crew, not about signing in`() {
        assertEquals(CashEligibility.NeedsCard(2), CashEligibility.resolve(signedIn = false, requiredEmployees = 2))
    }

    @Test
    fun `a guest is refused before any quote has named the crew`() {
        assertEquals(CashEligibility.NeedsAccount, CashEligibility.resolve(signedIn = false, requiredEmployees = null))
    }

    @Test
    fun `nothing is decided for a signed-in customer until a quote describes the selection`() {
        assertEquals(CashEligibility.Pending, CashEligibility.resolve(signedIn = true, requiredEmployees = null))
    }

    @Test
    fun `a crew figure the server never produces does not allow cash`() {
        assertNotEquals(CashEligibility.Available, CashEligibility.resolve(signedIn = true, requiredEmployees = 0))
    }

    @Test
    fun `only a verdict takes cash away — an unknown crew keeps the choice`() {
        assertTrue(CashEligibility.NeedsAccount.isRefused)
        assertTrue(CashEligibility.NeedsCard(2).isRefused)
        assertFalse(CashEligibility.Pending.isRefused)
        assertFalse(CashEligibility.Available.isRefused)
    }
}
