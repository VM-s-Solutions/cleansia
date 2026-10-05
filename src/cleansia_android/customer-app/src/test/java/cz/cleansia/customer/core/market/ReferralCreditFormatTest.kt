package cz.cleansia.customer.core.market

import java.util.Locale
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * A referral pays each side the market currency's `ReferralCredit`, and a null or zero figure pays
 * nothing, so the copy states the figure in that currency or falls to the variant that promises none.
 */
class ReferralCreditFormatTest {

    private fun market(credit: Double?, currency: String = "CZK") = MarketListItem(
        countryId = "cze-id",
        isoCode = "CZE",
        isoAlpha2 = "CZ",
        name = "Czechia",
        currencyId = "cur-$currency",
        currencyCode = currency,
        currencySymbol = currency,
        isDefault = true,
        referralCredit = credit,
    )

    @Test
    fun `the credit is stated in the market's own currency`() {
        assertEquals("150 Kč", market(150.0).formattedReferralCredit(Locale.forLanguageTag("cs-CZ")))
        assertEquals("150 Kč", market(150.0).formattedReferralCredit(Locale.ENGLISH))
        assertEquals("7.50 €", market(7.5, "EUR").formattedReferralCredit(Locale.ENGLISH))
    }

    @Test
    fun `a market with no credit states no figure`() {
        assertNull(market(null).formattedReferralCredit(Locale.ENGLISH))
    }

    @Test
    fun `a zero credit pays nothing, so it states no figure either`() {
        assertNull(market(0.0).formattedReferralCredit(Locale.ENGLISH))
    }
}
