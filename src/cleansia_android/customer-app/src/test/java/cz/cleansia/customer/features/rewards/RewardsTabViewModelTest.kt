package cz.cleansia.customer.features.rewards

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.loyalty.LoyaltyAccountDto
import cz.cleansia.customer.core.loyalty.LoyaltyActivityItemDto
import cz.cleansia.customer.core.loyalty.LoyaltyActivityResponseDto
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.loyalty.TierInfoDto
import cz.cleansia.customer.core.referral.ReferralAccountDto
import cz.cleansia.customer.core.referral.ReferralRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class RewardsTabViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var loyaltyRepository: LoyaltyRepository
    private lateinit var referralRepository: ReferralRepository
    private lateinit var catalogRepository: CatalogRepository
    private lateinit var marketRepository: cz.cleansia.customer.core.market.MarketRepository
    private lateinit var snackbar: SnackbarController
    private val marketState = MutableStateFlow<cz.cleansia.customer.core.market.MarketState>(
        cz.cleansia.customer.core.market.MarketState.Unavailable,
    )

    private val account = MutableStateFlow<LoyaltyAccountDto?>(null)
    private val tiers = MutableStateFlow<List<TierInfoDto>>(emptyList())
    private val loading = MutableStateFlow(false)
    private val loaded = MutableStateFlow(false)
    private val referralAccount = MutableStateFlow<ReferralAccountDto?>(null)
    private val currencyCode = MutableStateFlow<String?>(null)

    @Before
    fun setUp() {
        loyaltyRepository = mockk(relaxed = true)
        referralRepository = mockk(relaxed = true)
        catalogRepository = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        every { loyaltyRepository.account } returns account
        every { loyaltyRepository.tiers } returns tiers
        every { loyaltyRepository.loading } returns loading
        every { loyaltyRepository.loaded } returns loaded
        every { referralRepository.account } returns referralAccount
        every { catalogRepository.currencyCode } returns currencyCode
        marketRepository = mockk(relaxed = true)
        every { marketRepository.state } returns marketState
    }

    private fun viewModel() =
        RewardsTabViewModel(loyaltyRepository, referralRepository, catalogRepository, marketRepository, snackbar)

    private fun market(iso: String, currency: String, isDefault: Boolean) = cz.cleansia.customer.core.market.MarketListItem(
        countryId = "$iso-id",
        isoCode = iso,
        isoAlpha2 = iso.take(2),
        name = iso,
        currencyId = "$currency-id",
        currencyCode = currency,
        currencySymbol = currency,
        isDefault = isDefault,
    )

    private val cze = market("CZE", "CZK", isDefault = true)
    private val svk = market("SVK", "EUR", isDefault = false)

    // ADR-0058 D5: the tier floor is a platform-default-currency number enforced only on orders in
    // that currency, so the floor line renders only when the market's currency is the default.

    @Test
    fun `with no market the floor applies and is labelled with the catalogue default`() = runTest {
        currencyCode.value = "CZK"
        val vm = viewModel()
        runCurrent()

        assertEquals(true, vm.tierFloorApplies.value)
        assertEquals("CZK", vm.currencyCode.value)
    }

    @Test
    fun `in the default-currency market the floor applies and is labelled with that currency`() = runTest {
        marketState.value = cz.cleansia.customer.core.market.MarketState.Resolved(listOf(cze, svk), selected = cze)
        val vm = viewModel()
        runCurrent()

        assertEquals(true, vm.tierFloorApplies.value)
        assertEquals("CZK", vm.currencyCode.value)
    }

    @Test
    fun `in another market the floor line is omitted`() = runTest {
        marketState.value = cz.cleansia.customer.core.market.MarketState.Resolved(listOf(cze, svk), selected = svk)
        val vm = viewModel()
        runCurrent()

        assertEquals(false, vm.tierFloorApplies.value)
        assertEquals("EUR", vm.currencyCode.value)
    }

    @Test
    fun `switching the market moves the floor line without a restart`() = runTest {
        marketState.value = cz.cleansia.customer.core.market.MarketState.Resolved(listOf(cze, svk), selected = cze)
        val vm = viewModel()
        runCurrent()
        assertEquals(true, vm.tierFloorApplies.value)

        marketState.value = cz.cleansia.customer.core.market.MarketState.Resolved(listOf(cze, svk), selected = svk)
        runCurrent()

        assertEquals(false, vm.tierFloorApplies.value)
    }

    @Test
    fun `the exposed flows mirror the repositories`() = runTest {
        val vm = viewModel()

        val tier = mockk<TierInfoDto>()
        val loyalty = mockk<LoyaltyAccountDto>()
        val referral = mockk<ReferralAccountDto>()
        account.value = loyalty
        tiers.value = listOf(tier)
        loading.value = true
        loaded.value = true
        referralAccount.value = referral
        currencyCode.value = "EUR"
        runCurrent()

        assertEquals(loyalty, vm.account.value)
        assertEquals(listOf(tier), vm.tiers.value)
        assertEquals(true, vm.loading.value)
        assertEquals(true, vm.loaded.value)
        assertEquals(referral, vm.referralAccount.value)
        assertEquals("EUR", vm.currencyCode.value)
    }

    @Test
    fun `the activity preview holds the first five rows and a failure is snackbarred`() = runTest {
        val row = mockk<LoyaltyActivityItemDto>()
        coEvery { loyaltyRepository.loadActivity(offset = 0, limit = 5) } returns
            ApiResult.Success(LoyaltyActivityResponseDto(data = listOf(row), total = 1))

        val vm = viewModel()
        vm.loadActivityPreview()
        advanceUntilIdle()
        assertEquals(listOf(row), vm.activityPreview.value)

        coEvery { loyaltyRepository.loadActivity(offset = 0, limit = 5) } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "server boom"))
        vm.loadActivityPreview()
        advanceUntilIdle()

        assertEquals(listOf(row), vm.activityPreview.value)
        verify(exactly = 1) { snackbar.showError(match<ApiError> { it.getUserMessage() == "server boom" }) }
    }

    @Test
    fun `refresh reloads loyalty, the referral snapshot and the preview, silent on a transport error`() = runTest {
        coEvery { loyaltyRepository.refresh() } returns ApiResult.Error(ApiError.Network("offline"))
        coEvery { referralRepository.refresh() } returns ApiResult.Success(Unit)
        coEvery { loyaltyRepository.loadActivity(offset = 0, limit = 5) } returns
            ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        vm.refresh()
        advanceUntilIdle()

        coVerify(exactly = 1) { loyaltyRepository.refresh() }
        coVerify(exactly = 1) { referralRepository.refresh() }
        coVerify(exactly = 1) { loyaltyRepository.loadActivity(offset = 0, limit = 5) }
        verify(exactly = 0) { snackbar.showError(any<ApiError>()) }
    }

    @Test
    fun `the referral notices are keyed strings`() = runTest {
        val vm = viewModel()

        vm.onReferralCodeCopied()
        vm.onReferralShareUnavailable()

        verify(exactly = 1) { snackbar.showSuccessKey(R.string.loyalty_referral_copied_toast) }
        verify(exactly = 1) { snackbar.showInfoKey(R.string.loyalty_referral_share_failed) }
    }
}
