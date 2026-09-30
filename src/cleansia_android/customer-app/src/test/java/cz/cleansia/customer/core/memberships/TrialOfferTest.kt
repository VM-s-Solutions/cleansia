package cz.cleansia.customer.core.memberships

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * One free trial per account: the server sends a plan's own trial days to a customer who has never had
 * one and 0 to anyone who has. A surface that offered a trial on the plan alone would promise a
 * returning customer free days and then bill them on subscribe.
 */
class TrialOfferTest {

    private fun plan(code: String, billingInterval: Int, trialDays: Int) = MembershipPlanDto(
        code = code,
        name = code,
        price = 199.0,
        monthlyEquivalentPrice = 199.0,
        billingInterval = billingInterval,
        discountPercentage = 5.0,
        freeCancellationWindowHours = 4,
        allowsExpressUpgrade = true,
        trialPeriodDays = trialDays,
        savingsPercentVsMonthly = 0.0,
        currencyCode = "CZK",
    )

    private val eligible = GetMyMembershipResponse(hasMembership = false, trialEligible = true)
    private val hadTrial = GetMyMembershipResponse(hasMembership = false, trialEligible = false)

    private val monthly = plan("plus_monthly", billingInterval = 1, trialDays = 14)
    private val yearly = plan("plus_yearly", billingInterval = 2, trialDays = 30)

    @Test
    fun `a customer who never had a trial is offered the plan's own days`() {
        assertEquals(14, eligible.trialDaysOn(monthly))
        assertEquals(30, eligible.trialDaysOn(yearly))
    }

    @Test
    fun `a customer who has had their trial is offered none`() {
        assertEquals(0, hadTrial.trialDaysOn(monthly))
    }

    @Test
    fun `a plan without a trial offers none`() {
        assertEquals(0, eligible.trialDaysOn(monthly.copy(trialPeriodDays = 0)))
    }

    @Test
    fun `eligibility not read yet offers none`() {
        assertEquals(0, (null as GetMyMembershipResponse?).trialDaysOn(monthly))
    }

    @Test
    fun `no plan offers none`() {
        assertEquals(0, eligible.trialDaysOn(null))
    }

    @Test
    fun `a surface without a plan picker offers the monthly plan's trial`() {
        assertEquals(14, eligible.headlineTrialDays(listOf(yearly, monthly)))
    }

    @Test
    fun `with no monthly plan the headline trial is the first plan's`() {
        assertEquals(30, eligible.headlineTrialDays(listOf(yearly)))
    }

    @Test
    fun `no plans and no eligibility offer no headline trial`() {
        assertEquals(0, eligible.headlineTrialDays(emptyList()))
        assertEquals(0, hadTrial.headlineTrialDays(listOf(monthly, yearly)))
    }
}
