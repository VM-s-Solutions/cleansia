package cz.cleansia.customer.features.membership

import android.content.Context
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.memberships.CancelMembershipSubscriptionResponse
import cz.cleansia.customer.core.memberships.CreateMembershipSubscriptionResponse
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
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
    private lateinit var catalogRepository: CatalogRepository
    private lateinit var appContext: Context
    private val current = MutableStateFlow<GetMyMembershipResponse?>(null)
    private val loading = MutableStateFlow(false)
    private val catalogCurrency = MutableStateFlow<String?>(null)

    @Before
    fun setUp() {
        repository = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { repository.current } returns current
        every { repository.loading } returns loading
        catalogRepository = mockk(relaxed = true)
        every { catalogRepository.currencyCode } returns catalogCurrency
        coEvery { repository.refresh() } returns ApiResult.Error(ApiError.Network("network error"))
        coEvery { repository.getPlans() } returns ApiResult.Success(emptyList())
        every { appContext.getString(R.string.error_generic_network) } returns "network error"
    }

    private fun viewModel() = MembershipViewModel(repository, snackbar, catalogRepository, appContext)

    @Test
    fun `submit starts Idle`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    /**
     * Neither a plan nor the membership arrives with a currency, so every figure on the Plus surfaces
     * is labelled with the platform default the catalogue resolved — never a koruna literal.
     */
    @Test
    fun `currencyCode mirrors the catalogue default`() = runTest {
        val vm = viewModel()
        assertEquals(null, vm.currencyCode.value)

        catalogCurrency.value = "EUR"
        advanceUntilIdle()

        assertEquals("EUR", vm.currencyCode.value)
    }

    @Test
    fun `startSubscribe success returns NeedsPaymentMethod and Idle`() = runTest {
        coEvery { repository.subscribePhase1("plus_monthly") } returns ApiResult.Success(
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
        coEvery { repository.subscribePhase1("plus_monthly") } returns
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
        coEvery { repository.subscribePhase1("plus_monthly") } returns
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
        coEvery { repository.subscribePhase1("plus_monthly") } returns ApiResult.Success(
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
        coEvery { repository.subscribePhase2("plus_monthly", any()) } returns ApiResult.Success(
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
