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
    private lateinit var snackbar: SnackbarController

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
    }

    private fun viewModel() = RewardsTabViewModel(loyaltyRepository, referralRepository, catalogRepository, snackbar)

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
