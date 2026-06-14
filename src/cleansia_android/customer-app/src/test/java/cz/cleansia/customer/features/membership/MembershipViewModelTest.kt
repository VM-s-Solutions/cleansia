package cz.cleansia.customer.features.membership

import android.content.Context
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
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
    private lateinit var appContext: Context
    private val current = MutableStateFlow<GetMyMembershipResponse?>(null)
    private val loading = MutableStateFlow(false)

    @Before
    fun setUp() {
        repository = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { repository.current } returns current
        every { repository.loading } returns loading
        coEvery { repository.refresh() } returns null
        coEvery { repository.getPlans() } returns emptyList()
        every { appContext.getString(R.string.error_generic_network) } returns "network error"
    }

    private fun viewModel() = MembershipViewModel(repository, snackbar, appContext)

    @Test
    fun `submit starts Idle`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `startSubscribe success returns NeedsPaymentMethod and Idle`() = runTest {
        coEvery { repository.subscribePhase1("plus_monthly") } returns
            CreateMembershipSubscriptionResponse(
                membershipId = "",
                setupIntentClientSecret = "seti_secret",
                stripeCustomerId = "cus_1",
                ephemeralKey = "ek_1",
            )

        val vm = viewModel()
        advanceUntilIdle()
        val outcome = vm.startSubscribe("plus_monthly")

        assertTrue(outcome is SubscribeOutcome.NeedsPaymentMethod)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `startSubscribe failure snackbars, returns Failed and Idle`() = runTest {
        coEvery { repository.subscribePhase1("plus_monthly") } returns null

        val vm = viewModel()
        advanceUntilIdle()
        val outcome = vm.startSubscribe("plus_monthly")

        assertEquals(SubscribeOutcome.Failed, outcome)
        verify { snackbar.showError("network error") }
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `startSubscribe with existing membership returns AlreadyActive and Idle`() = runTest {
        coEvery { repository.subscribePhase1("plus_monthly") } returns
            CreateMembershipSubscriptionResponse(
                membershipId = "mem-existing",
                setupIntentClientSecret = "seti",
                stripeCustomerId = "cus",
                ephemeralKey = "ek",
            )

        val vm = viewModel()
        advanceUntilIdle()
        val outcome = vm.startSubscribe("plus_monthly")

        assertEquals(SubscribeOutcome.AlreadyActive, outcome)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `confirmSubscribe success returns Subscribed and Idle`() = runTest {
        coEvery { repository.subscribePhase2("plus_monthly", any()) } returns
            CreateMembershipSubscriptionResponse(
                membershipId = "mem-99",
                setupIntentClientSecret = "",
                stripeCustomerId = "cus",
                ephemeralKey = "ek",
            )

        val vm = viewModel()
        advanceUntilIdle()
        val outcome = vm.confirmSubscribe("plus_monthly")

        assertTrue(outcome is SubscribeOutcome.Subscribed)
        assertEquals("mem-99", (outcome as SubscribeOutcome.Subscribed).membershipId)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `cancel success runs callback and returns to Idle`() = runTest {
        coEvery { repository.cancel() } returns
            CancelMembershipSubscriptionResponse(membershipId = "mem-1", effectiveEndDate = "2026-07-01")

        var endDate: String? = null
        val vm = viewModel()
        advanceUntilIdle()
        vm.cancel { endDate = it }
        advanceUntilIdle()

        assertEquals("2026-07-01", endDate)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `cancel failure snackbars and returns to Idle`() = runTest {
        coEvery { repository.cancel() } returns null

        val vm = viewModel()
        advanceUntilIdle()
        vm.cancel { }
        advanceUntilIdle()

        verify { snackbar.showError("network error") }
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `swapPlan success runs callback`() = runTest {
        coEvery { repository.swapPlan("plus_yearly") } returns
            SwapMembershipPlanResponse(
                membershipId = "mem-1",
                newPlanCode = "plus_yearly",
                currentPeriodEnd = "2026-12-01",
            )

        var swapped = false
        val vm = viewModel()
        advanceUntilIdle()
        vm.swapPlan("plus_yearly") { swapped = true }
        advanceUntilIdle()

        assertTrue(swapped)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }
}
