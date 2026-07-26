package cz.cleansia.partner.features.orders

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import java.util.Locale

/**
 * Pins the amount shown in the "mark cash as collected" confirmation.
 *
 * The dialog names the sum the cleaner is about to record as taken, and that
 * record is irreversible — there is no un-collect endpoint. Naming a wrong or
 * absent amount is therefore worse than naming none at all, so anything that
 * isn't a real, positive total must fall through to the amount-free copy.
 *
 * iOS pins the same rule in `OrderDetail.cashDueLabel`; this is the Android
 * twin. Locale is passed explicitly so grouping separators don't make the
 * assertions depend on the machine running them.
 */
class CashDueLabelTest {

    @Test
    fun `a positive total renders with its currency`() {
        assertEquals("1,200 Kč", cashDueLabel(1200.0, "CZK", Locale.US))
        assertEquals("80 €", cashDueLabel(80.0, "EUR", Locale.US))
    }

    @Test
    fun `a missing total asks without naming an amount`() {
        assertNull(cashDueLabel(null, "CZK", Locale.US))
    }

    @Test
    fun `a zero or negative total asks without naming an amount`() {
        // "Confirm you have taken 0 Kč in cash" is worse than not asking for a
        // number at all — the cleaner would be confirming a nonsense figure.
        assertNull(cashDueLabel(0.0, "CZK", Locale.US))
        assertNull(cashDueLabel(-50.0, "CZK", Locale.US))
    }

    @Test
    fun `a missing currency code falls back to the platform default`() {
        assertEquals("500 Kč", cashDueLabel(500.0, null, Locale.US))
    }
}
