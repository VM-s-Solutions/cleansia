package cz.cleansia.customer.features.auth

import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.market.MarketListItem
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.core.referral.ReferralRepository
import cz.cleansia.customer.core.referral.ValidateReferralResponse
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import org.junit.Before
import org.junit.Test

/**
 * The sign-up form validates a referral code before there is an account, so the request has to name
 * the market the code is looked up in (ADR-0061 D3) — the same persisted choice `register` sends.
 */
class SignUpViewModelTest {

    private lateinit var referralRepository: ReferralRepository
    private lateinit var marketRepository: MarketRepository

    @Before
    fun setUp() {
        referralRepository = mockk()
        marketRepository = mockk()
        coEvery { referralRepository.validate(any(), any()) } returns
            ApiResult.Success(ValidateReferralResponse(isValid = true, referrerFirstName = "Bob"))
    }

    private fun viewModel() = SignUpViewModel(referralRepository, marketRepository)

    @Test
    fun `validateReferral names the persisted market`() = runTest {
        val svk = MarketListItem(
            countryId = "svk-id",
            isoCode = "SVK",
            isoAlpha2 = "SK",
            name = "Slovakia",
            currencyId = "cur-eur",
            currencyCode = "EUR",
            currencySymbol = "€",
            isDefault = false,
        )
        coEvery { marketRepository.ensureLoaded() } returns MarketState.Resolved(listOf(svk), svk)

        viewModel().validateReferral("FRIEND10")

        coVerify(exactly = 1) { referralRepository.validate("FRIEND10", "svk-id") }
    }

    @Test
    fun `validateReferral with no market known sends none`() = runTest {
        coEvery { marketRepository.ensureLoaded() } returns MarketState.Unavailable

        viewModel().validateReferral("FRIEND10")

        coVerify(exactly = 1) { referralRepository.validate("FRIEND10", null) }
    }
}
