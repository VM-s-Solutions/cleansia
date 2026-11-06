package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.CurrencyDetailDto
import cz.cleansia.partner.api.model.OrderItem
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Test
import java.util.Locale

/**
 * Pins the amounts the two cash confirmations name: [cashDueText] in "mark cash as collected", written as the
 * pinned button writes it, and [cashDueLabel] in the customer-did-not-pay report.
 *
 * Both records are irreversible for the cleaner, so naming a wrong or absent amount is worse than naming none at
 * all: anything that isn't a real, positive amount falls through to the amount-free copy. Both write it in the
 * board's money format, as the pinned button does.
 *
 * iOS pins the same rule in `OrderDetail.cashDueLabel`. Locale is passed explicitly so grouping separators don't
 * make the assertions depend on the machine running them.
 */
class CashDueLabelTest {

    private fun due(amount: Double?) = OrderItem(amountDueOnCard = amount, currency = CurrencyDetailDto(code = "CZK", symbol = "Kč"))

    @Test
    fun `the mark-collected confirm writes the amount as the pinned button does`() {
        assertEquals("1\u00a0240\u00a0Kč", due(1240.0).cashDueText(Locale.US))
        assertEquals(OrdersBoardRules.money(1240.0, "Kč", Locale.US), due(1240.0).cashDueText(Locale.US))
    }

    @Test
    fun `the mark-collected confirm names no amount for none, zero or less`() {
        assertNull(due(null).cashDueText(Locale.US))
        assertNull(due(0.0).cashDueText(Locale.US))
        assertNull(due(-50.0).cashDueText(Locale.US))
    }

    @Test
    fun `the not-paid confirm writes the amount as the board and the mark-collected confirm do`() {
        assertEquals("1\u00a0240\u00a0Kč", cashDueLabel(1240.0, "Kč", Locale.US))
        assertEquals("80\u00a0€", cashDueLabel(80.0, "€", Locale.US))
    }

    @Test
    fun `a missing total asks without naming an amount`() {
        assertNull(cashDueLabel(null, "Kč", Locale.US))
    }

    @Test
    fun `a zero or negative total asks without naming an amount`() {
        assertNull(cashDueLabel(0.0, "Kč", Locale.US))
        assertNull(cashDueLabel(-50.0, "Kč", Locale.US))
    }

    @Test
    fun `a blank symbol takes the currency code's, as the board's cash flag does`() {
        val blank = OrderItem(amountDueOnCard = 1240.0, paymentType = Code(value = 1), currency = CurrencyDetailDto(code = "CZK", symbol = " "))
        val flag = OrdersBoardRules.cashFlag(blank.asBoardRow(), Locale.US) as BoardText.Res
        assertEquals(flag.args.single(), blank.cashDueText(Locale.US))
        assertNotEquals("1\u00a0240", blank.cashDueText(Locale.US))
    }

    /** A missing symbol is not a koruna order: the figure is named, its unit is not guessed. */
    @Test
    fun `a missing currency symbol names the amount without a unit`() {
        assertEquals("500", cashDueLabel(500.0, null, Locale.US))
    }
}
