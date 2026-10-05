package cz.cleansia.customer.features.auth

import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.market.MarketListItem
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.core.referral.ReferralRepository
import cz.cleansia.customer.core.referral.ValidateReferralResponse
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The sign-up form validates a referral code before there is an account, so the request has to name
 * the market the code is looked up in (ADR-0061 D3) — the same persisted choice `register` sends.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class SignUpViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var referralRepository: ReferralRepository
    private lateinit var marketRepository: MarketRepository
    private val marketState = MutableStateFlow<MarketState>(MarketState.Unavailable)

    private val svk = MarketListItem(
        countryId = "svk-id",
        isoCode = "SVK",
        isoAlpha2 = "SK",
        name = "Slovakia",
        currencyId = "cur-eur",
        currencyCode = "EUR",
        currencySymbol = "€",
        isDefault = false,
        referralCredit = 7.5,
    )

    @Before
    fun setUp() {
        referralRepository = mockk()
        marketRepository = mockk()
        every { marketRepository.state } returns marketState
        coEvery { marketRepository.ensureLoaded() } returns MarketState.Unavailable
        coEvery { referralRepository.validate(any(), any()) } returns
            ApiResult.Success(ValidateReferralResponse(isValid = true, referrerFirstName = "Bob"))
    }

    private fun viewModel() = SignUpViewModel(referralRepository, marketRepository)

    @Test
    fun `validateReferral names the persisted market`() = runTest {
        coEvery { marketRepository.ensureLoaded() } returns MarketState.Resolved(listOf(svk), svk)

        viewModel().validateReferral("FRIEND10")

        coVerify(exactly = 1) { referralRepository.validate("FRIEND10", "svk-id") }
    }

    @Test
    fun `validateReferral with no market known sends none`() = runTest {
        viewModel().validateReferral("FRIEND10")

        coVerify(exactly = 1) { referralRepository.validate("FRIEND10", null) }
    }

    /**
     * The code sheet states the referral credit of the chosen market before any code is checked, and
     * sign-up comes before the shell that would otherwise have read the directory.
     */
    @Test
    fun `opening sign-up reads the directory and exposes the chosen market`() = runTest {
        val vm = viewModel()
        runCurrent()
        coVerify(exactly = 1) { marketRepository.ensureLoaded() }
        assertEquals(null, vm.selectedMarket.value)

        marketState.value = MarketState.Resolved(listOf(svk), svk)
        runCurrent()

        assertEquals(svk, vm.selectedMarket.value)
    }
}
