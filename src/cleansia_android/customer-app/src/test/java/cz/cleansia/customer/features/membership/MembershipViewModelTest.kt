package cz.cleansia.customer.features.membership

import android.content.Context
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.market.MarketListItem
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.core.memberships.CancelMembershipSubscriptionResponse
import cz.cleansia.customer.core.memberships.CreateMembershipSubscriptionResponse
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.MembershipPlanDto
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.memberships.SwapMembershipPlanResponse
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
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
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class MembershipViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: MembershipRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var marketRepository: MarketRepository
    private lateinit var appContext: Context
    private val current = MutableStateFlow<GetMyMembershipResponse?>(null)
    private val loading = MutableStateFlow(false)
    private val marketState = MutableStateFlow<MarketState>(MarketState.Unavailable)

    @Before
    fun setUp() {
        repository = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { repository.current } returns current
        every { repository.loading } returns loading
        marketRepository = mockk(relaxed = true)
        every { marketRepository.state } returns marketState
        coEvery { marketRepository.ensureLoaded() } answers { marketState.value }
        coEvery { repository.refresh() } returns ApiResult.Error(ApiError.Network("network error"))
        coEvery { repository.getPlans(any()) } returns ApiResult.Success(emptyList())
        every { appContext.getString(R.string.error_generic_network) } returns "network error"
    }

    private fun viewModel() = MembershipViewModel(repository, snackbar, marketRepository, appContext)

    private fun market(iso: String, currency: String, isDefault: Boolean = false) = MarketListItem(
        countryId = "$iso-id",
        isoCode = iso,
        isoAlpha2 = iso.take(2),
        name = iso,
        currencyId = "$currency-id",
        currencyCode = currency,
        currencySymbol = currency,
        isDefault = isDefault,
    )

    private fun plan(code: String, currency: String) = MembershipPlanDto(
        code = code,
        name = code,
        price = 199.0,
        monthlyEquivalentPrice = 199.0,
        billingInterval = 1,
        discountPercentage = 10.0,
        freeCancellationWindowHours = 24,
        allowsExpressUpgrade = true,
        trialPeriodDays = 0,
        savingsPercentVsMonthly = 0.0,
        currencyCode = currency,
    )

    private val cze = market("CZE", "CZK", isDefault = true)
    private val svk = market("SVK", "EUR")

    @Test
    fun `submit starts Idle`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    // ADR-0059 D3: the plans are asked for in the chosen market, and follow it when it changes.

    @Test
    fun `plans are requested for the chosen market once it has resolved`() = runTest {
        marketState.value = MarketState.Resolved(listOf(cze, svk), selected = svk)
        coEvery { repository.getPlans("SVK-id") } returns ApiResult.Success(listOf(plan("plus_monthly", "EUR")))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(listOf("plus_monthly"), vm.plans.value.map { it.code })
        assertEquals("EUR", vm.plans.value.single().currencyCode)
        coVerify(exactly = 1) { repository.getPlans("SVK-id") }
        coVerify(exactly = 0) { repository.getPlans(null) }
    }

    @Test
    fun `with no market the plans are requested for the platform default`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        coVerify(exactly = 1) { repository.getPlans(null) }
        assertEquals(emptyList<MembershipPlanDto>(), vm.plans.value)
    }

    @Test
    fun `switching the market re-requests the plans for it without a restart`() = runTest {
        marketState.value = MarketState.Resolved(listOf(cze, svk), selected = cze)
        coEvery { repository.getPlans("CZE-id") } returns ApiResult.Success(listOf(plan("plus_monthly", "CZK")))
        coEvery { repository.getPlans("SVK-id") } returns ApiResult.Success(emptyList())
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals("CZK", vm.plans.value.single().currencyCode)

        marketState.value = MarketState.Resolved(listOf(cze, svk), selected = svk)
        advanceUntilIdle()

        assertEquals(emptyList<MembershipPlanDto>(), vm.plans.value)
        coVerify(exactly = 1) { repository.getPlans("SVK-id") }
    }

    @Test
    fun `an empty plan list is Plus not on sale in the market, not a failure`() = runTest {
        marketState.value = MarketState.Resolved(listOf(cze, svk), selected = svk)
        coEvery { repository.getPlans("SVK-id") } returns ApiResult.Success(emptyList())

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(true, vm.plansLoaded.value)
        assertEquals(emptyList<MembershipPlanDto>(), vm.plans.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `a failed plan read is not mistaken for an empty market`() = runTest {
        coEvery { repository.getPlans(any()) } returns ApiResult.Error(ApiError.Network("network error"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(false, vm.plansLoaded.value)
        assertEquals(emptyList<MembershipPlanDto>(), vm.plans.value)
    }

    // ADR-0059 D2: both subscribe phases carry the chosen market's country.

    @Test
    fun `both subscribe phases send the chosen market's country`() = runTest {
        marketState.value = MarketState.Resolved(listOf(cze, svk), selected = svk)
        coEvery { repository.subscribePhase1("plus_monthly", "SVK-id") } returns ApiResult.Success(
            CreateMembershipSubscriptionResponse("", "seti", "cus", "ek"),
        )
        coEvery { repository.subscribePhase2("plus_monthly", any(), "SVK-id") } returns ApiResult.Success(
            CreateMembershipSubscriptionResponse("mem-1", "", "cus", "ek"),
        )

        val vm = viewModel()
        advanceUntilIdle()
        val first = vm.startSubscribe("plus_monthly")
        val second = vm.confirmSubscribe("plus_monthly")

        assertTrue(first is SubscribeOutcome.NeedsPaymentMethod)
        assertTrue(second is SubscribeOutcome.Subscribed)
        coVerify(exactly = 1) { repository.subscribePhase1("plus_monthly", "SVK-id") }
        coVerify(exactly = 1) { repository.subscribePhase2("plus_monthly", any(), "SVK-id") }
    }

    @Test
    fun `startSubscribe success returns NeedsPaymentMethod and Idle`() = runTest {
        coEvery { repository.subscribePhase1("plus_monthly", null) } returns ApiResult.Success(
            CreateMembershipSubscriptionResponse(
                membershipId = "",
                setupIntentClientSecret = "seti_secret",
                stripeCustomerId = "cus_1",
                ephemeralKey = "ek_1",
            ),
        )

        val vm = viewModel()
        advanceUntilIdle()
        val outcome = vm.startSubscribe("plus_monthly")

        assertTrue(outcome is SubscribeOutcome.NeedsPaymentMethod)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `startSubscribe http failure snackbars carried message, returns Failed and Idle`() = runTest {
        coEvery { repository.subscribePhase1("plus_monthly", null) } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "server boom"))

        val vm = viewModel()
        advanceUntilIdle()
        val outcome = vm.startSubscribe("plus_monthly")

        assertEquals(SubscribeOutcome.Failed, outcome)
        verify { snackbar.showError(match<ApiError> { it.getUserMessage() == "server boom" }) }
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `startSubscribe network failure stays silent, returns Failed and Idle`() = runTest {
        coEvery { repository.subscribePhase1("plus_monthly", null) } returns
            ApiResult.Error(ApiError.Network("network error"))

        val vm = viewModel()
        advanceUntilIdle()
        val outcome = vm.startSubscribe("plus_monthly")

        assertEquals(SubscribeOutcome.Failed, outcome)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `startSubscribe with existing membership returns AlreadyActive and Idle`() = runTest {
        coEvery { repository.subscribePhase1("plus_monthly", null) } returns ApiResult.Success(
            CreateMembershipSubscriptionResponse(
                membershipId = "mem-existing",
                setupIntentClientSecret = "seti",
                stripeCustomerId = "cus",
                ephemeralKey = "ek",
            ),
        )

        val vm = viewModel()
        advanceUntilIdle()
        val outcome = vm.startSubscribe("plus_monthly")

        assertEquals(SubscribeOutcome.AlreadyActive, outcome)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `confirmSubscribe success returns Subscribed and Idle`() = runTest {
        coEvery { repository.subscribePhase2("plus_monthly", any(), null) } returns ApiResult.Success(
            CreateMembershipSubscriptionResponse(
                membershipId = "mem-99",
                setupIntentClientSecret = "",
                stripeCustomerId = "cus",
                ephemeralKey = "ek",
            ),
        )

        val vm = viewModel()
        advanceUntilIdle()
        val outcome = vm.confirmSubscribe("plus_monthly")

        assertTrue(outcome is SubscribeOutcome.Subscribed)
        assertEquals("mem-99", (outcome as SubscribeOutcome.Subscribed).membershipId)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `cancel success snackbars the period end and returns to Idle`() = runTest {
        coEvery { repository.cancel() } returns ApiResult.Success(
            CancelMembershipSubscriptionResponse(effectiveEndDate = "2026-07-01T00:00:00Z"),
        )
        every { appContext.getString(R.string.membership_cancelled_until, any()) } answers {
            "Active until ${secondArg<Array<Any?>>()[0]}"
        }

        val vm = viewModel()
        advanceUntilIdle()
        vm.cancel()
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showSuccess("Active until ${formatPeriodEnd("2026-07-01T00:00:00Z")}") }
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `cancel http failure snackbars carried message and returns to Idle`() = runTest {
        coEvery { repository.cancel() } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "server boom"))

        val vm = viewModel()
        advanceUntilIdle()
        vm.cancel()
        advanceUntilIdle()

        verify { snackbar.showError(match<ApiError> { it.getUserMessage() == "server boom" }) }
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `cancel network failure stays silent and returns to Idle`() = runTest {
        coEvery { repository.cancel() } returns
            ApiResult.Error(ApiError.Network("network error"))

        val vm = viewModel()
        advanceUntilIdle()
        vm.cancel()
        advanceUntilIdle()

        verify(exactly = 0) { snackbar.showError(any<String>()) }
        verify(exactly = 0) { snackbar.showSuccess(any<String>()) }
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `swapPlan success snackbars the switch`() = runTest {
        coEvery { repository.swapPlan("plus_yearly") } returns ApiResult.Success(
            SwapMembershipPlanResponse(
                newPlanCode = "plus_yearly",
                currentPeriodEnd = "2026-12-01",
            ),
        )

        val vm = viewModel()
        advanceUntilIdle()
        vm.swapPlan("plus_yearly")
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showSuccessKey(R.string.membership_switch_success) }
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `swapPlan failure snackbars nothing as success`() = runTest {
        coEvery { repository.swapPlan("plus_yearly") } returns
            ApiResult.Error(ApiError.Network("network error"))

        val vm = viewModel()
        advanceUntilIdle()
        vm.swapPlan("plus_yearly")
        advanceUntilIdle()

        verify(exactly = 0) { snackbar.showSuccessKey(any()) }
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `the payment-sheet outcomes are keyed notices, a failure carries Stripe's own message`() = runTest {
        every { appContext.getString(R.string.error_payment_failed) } returns "Payment failed."
        val vm = viewModel()

        vm.onPaymentCancelled()
        vm.onPaymentFailed("Card declined")
        vm.onPaymentFailed(null)
        vm.onAlreadyActive()

        verify(exactly = 1) { snackbar.showErrorKey(R.string.error_payment_cancelled) }
        verify(exactly = 1) { snackbar.showError("Card declined") }
        verify(exactly = 1) { snackbar.showError("Payment failed.") }
        verify(exactly = 1) { snackbar.showSuccessKey(R.string.membership_already_active) }
    }
}
