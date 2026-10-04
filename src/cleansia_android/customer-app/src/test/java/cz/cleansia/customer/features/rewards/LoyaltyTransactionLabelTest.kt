package cz.cleansia.customer.features.rewards

import cz.cleansia.customer.R
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Every refund clawback, partial or full, is written as a Revoke with source `OrderPartiallyRefunded`
 * (wire 5) by `LoyaltyService.RevokeForRefundAsync`. It is a refund, so it reads as one — not as a
 * cancellation, and not as an admin's manual adjustment.
 */
class LoyaltyTransactionLabelTest {

    @Test
    fun `a refund clawback reads as a refund`() {
        assertEquals(R.string.loyalty_tx_refund_order, transactionLabelRes(5))
    }

    @Test
    fun `a cancellation reads as a cancellation`() {
        assertEquals(R.string.loyalty_tx_revoke_order, transactionLabelRes(2))
    }

    @Test
    fun `a completed order reads as earned`() {
        assertEquals(R.string.loyalty_tx_earn_order, transactionLabelRes(1))
    }

    @Test
    fun `a referral reads as a referral`() {
        assertEquals(R.string.loyalty_tx_referral, transactionLabelRes(3))
    }

    @Test
    fun `an admin's grant or take-back, and a source this app has no name for, read as a manual adjustment`() {
        assertEquals(R.string.loyalty_tx_manual, transactionLabelRes(4))
        assertEquals(R.string.loyalty_tx_manual, transactionLabelRes(6))
        assertEquals(R.string.loyalty_tx_manual, transactionLabelRes(99))
    }

    @Test
    fun `the refund line is written in all five locales with the points and the order number`() {
        listOf("values", "values-cs", "values-sk", "values-uk", "values-ru").forEach { locale ->
            val xml = File(moduleDir, "src/main/res/$locale/strings.xml").readText()
            val value = Regex("""<string name="loyalty_tx_refund_order">([^<]*)</string>""")
                .find(xml)?.groupValues?.get(1)
            assertTrue("$locale is missing loyalty_tx_refund_order", !value.isNullOrBlank())
            assertTrue("$locale must carry the points", value!!.contains("%1\$d"))
            assertTrue("$locale must carry the order number", value.contains("%2\$s"))
        }
    }

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")
}
