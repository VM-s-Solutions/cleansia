package cz.cleansia.partner.features.profile

import cz.cleansia.partner.api.model.MyPayoutDetails
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class PayoutAccountSummaryTest {

    @Test
    fun `local parts render the way the bank statement does`() {
        val summary = payoutAccountSummary(
            MyPayoutDetails(accountNumber = "5885638003", bankCode = "5500"),
        )

        assertEquals("5885638003/5500", summary)
    }

    @Test
    fun `the stored zero padding is stripped from both the prefix and the number`() {
        val summary = payoutAccountSummary(
            MyPayoutDetails(
                accountPrefix = "000019",
                accountNumber = "0002000145",
                bankCode = "0800",
            ),
        )

        assertEquals("19-2000145/0800", summary)
    }

    @Test
    fun `an all-zero prefix is not a prefix`() {
        val summary = payoutAccountSummary(
            MyPayoutDetails(accountPrefix = "000000", accountNumber = "5885638003", bankCode = "5500"),
        )

        assertEquals("5885638003/5500", summary)
    }

    @Test
    fun `an account with no local parts falls back to the iban`() {
        val summary = payoutAccountSummary(
            MyPayoutDetails(iban = "DE89370400440532013000"),
        )

        assertEquals("DE89370400440532013000", summary)
    }

    @Test
    fun `local parts win over the derived iban`() {
        val summary = payoutAccountSummary(
            MyPayoutDetails(
                accountNumber = "5885638003",
                bankCode = "5500",
                iban = "CZ3155000000005885638003",
            ),
        )

        assertEquals("5885638003/5500", summary)
    }

    @Test
    fun `no details at all is no summary`() {
        assertNull(payoutAccountSummary(null))
        assertNull(payoutAccountSummary(MyPayoutDetails()))
    }
}
