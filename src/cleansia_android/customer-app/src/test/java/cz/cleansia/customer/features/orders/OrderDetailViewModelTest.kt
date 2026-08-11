package cz.cleansia.customer.features.orders

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.notifications.OrderEvent
import cz.cleansia.customer.core.notifications.OrderEventBus
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.user.CodeDto
import cz.cleansia.customer.features.recurring.RecurringAuthoringGate
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * Covers the two behaviours the Order detail screen depends on but that are
 * invisible to a compile check: the safety-net poller's arm/stop rule, and
 * `load()`'s decision to blank the screen or keep the current order on it.
 *
 * Two hard rules for anything added here:
 *
 *  1. The poller is a `while (true) { delay(...) }` inside `viewModelScope`,
 *     which is *not* a child of the `runTest` scope. `advanceUntilIdle()` while
 *     it is armed therefore never returns — it keeps draining an infinite
 *     stream of scheduled ticks. Use `runCurrent()` to settle the initial load
 *     and a bounded `advanceTimeBy(...)` to step ticks, then
 *     `vm.viewModelScope.cancel()` before the test ends.
 *  2. `advanceTimeBy` does not run work scheduled at exactly
 *     `currentTime + delayTime`, so stepping N ticks needs
 *     `N * POLL_INTERVAL_MS + 1`.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class OrderDetailViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: OrderRepository
    private lateinit var membershipRepository: MembershipRepository
    private lateinit var membershipStaleness: Staleness
    private lateinit var membership: MutableStateFlow<GetMyMembershipResponse?>
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context
    private lateinit var orderEventBus: OrderEventBus

    /** Mirrors the VM's private companion constant — 5 minutes. */
    private val pollIntervalMs = 5L * 60L * 1000L

    private val orderId = "order-1"

    @Before
    fun setUp() {
        repository = mockk(relaxed = true)
        membershipRepository = mockk(relaxed = true)
        membershipStaleness = Staleness()
        membership = MutableStateFlow(null)
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        orderEventBus = OrderEventBus()

        every { membershipRepository.current } returns membership
        every { membershipRepository.staleness } returns membershipStaleness
        coEvery { membershipRepository.refresh() } coAnswers { membershipAnswer(hasMembership = false) }
    }

    /** Stands in for the repository writing its cache from a successful fetch. */
    private fun membershipAnswer(hasMembership: Boolean): ApiResult<GetMyMembershipResponse> {
        val body = GetMyMembershipResponse(hasMembership = hasMembership)
        membership.value = body
        membershipStaleness.markFresh()
        return ApiResult.Success(body)
    }

    private fun viewModel(id: String? = orderId) = OrderDetailViewModel(
        orderRepository = repository,
        snackbar = snackbar,
        appContext = appContext,
        savedStateHandle = SavedStateHandle(mapOf("orderId" to id)),
        membershipRepository = membershipRepository,
        orderEventBus = orderEventBus,
    )

    /** Wire values: Confirmed=2, OnTheWay=3, InProgress=4, Completed=5, Cancelled=6. */
    private fun order(statusValue: Int, notes: String? = null) = OrderDetailDto(
        id = orderId,
        totalPrice = 4380.0,
        originalSubtotal = 3650.0,
        appliedDiscountSource = 2,
        notes = notes,
        orderStatus = CodeDto(type = "OrderStatus", name = "status-$statusValue", value = statusValue),
    )

    private fun loadedOrder(vm: OrderDetailViewModel): OrderDetailDto =
        (vm.state.value as OrderDetailUiState.Loaded).order

    // ── poller arm / stop rule ──

    @Test
    fun `poller keeps ticking while the order is InProgress`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(4))

        val vm = viewModel()
        runCurrent()

        advanceTimeBy(2 * pollIntervalMs + 1)
        vm.viewModelScope.cancel()

        // 1 initial load + 2 ticks. Before the fix the loop broke on the first
        // tick because InProgress (4) was missing from its keep-going list, so
        // an order polled exactly once and then never again for the whole clean.
        coVerify(exactly = 3) { repository.getById(orderId) }
    }

    @Test
    fun `poller keeps ticking while the order is OnTheWay`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(3))

        val vm = viewModel()
        runCurrent()

        advanceTimeBy(2 * pollIntervalMs + 1)
        vm.viewModelScope.cancel()

        coVerify(exactly = 3) { repository.getById(orderId) }
    }

    @Test
    fun `poller stops once the order reaches a terminal status`() = runTest {
        var calls = 0
        coEvery { repository.getById(orderId) } coAnswers {
            ApiResult.Success(order(if (calls++ == 0) 2 else 5))
        }

        val vm = viewModel()
        runCurrent()

        advanceTimeBy(3 * pollIntervalMs + 1)
        vm.viewModelScope.cancel()

        // Initial load (Confirmed) arms the poller; the first tick returns
        // Completed, which must stop it. This pins the other half of the fix —
        // widening the keep-going condition must not turn it into a loop that
        // polls a finished order forever.
        coVerify(exactly = 2) { repository.getById(orderId) }
        assertEquals(5, loadedOrder(vm).orderStatus?.value)
    }

    @Test
    fun `poller never arms for a terminal order`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))

        val vm = viewModel()
        runCurrent()

        advanceTimeBy(3 * pollIntervalMs + 1)
        vm.viewModelScope.cancel()

        coVerify(exactly = 1) { repository.getById(orderId) }
    }

    @Test
    fun `a poll tick that fails leaves the loaded order on screen`() = runTest {
        var calls = 0
        coEvery { repository.getById(orderId) } coAnswers {
            if (calls++ == 0) {
                ApiResult.Success(order(4, notes = "original"))
            } else {
                ApiResult.Error(ApiError.Network("offline"))
            }
        }

        val vm = viewModel()
        runCurrent()

        advanceTimeBy(pollIntervalMs + 1)
        vm.viewModelScope.cancel()

        assertTrue(vm.state.value is OrderDetailUiState.Loaded)
        assertEquals("original", loadedOrder(vm).notes)
    }

    // ── load() keeps content on a refetch ──

    @Test
    fun `a push-triggered refetch keeps the current order on screen`() = runTest {
        val gate = CompletableDeferred<ApiResult<OrderDetailDto>>()
        var calls = 0
        coEvery { repository.getById(orderId) } coAnswers {
            if (calls++ == 0) ApiResult.Success(order(5, notes = "original")) else gate.await()
        }

        val vm = viewModel()
        advanceUntilIdle()
        assertTrue(vm.state.value is OrderDetailUiState.Loaded)

        orderEventBus.emit(OrderEvent(orderId = orderId, eventKey = "order.completed"))
        runCurrent()

        // The refetch is in flight. Before the fix `load()` set Loading
        // unconditionally, so every push blanked the whole screen to a
        // full-page spinner and threw away content that was still valid.
        assertTrue(
            "a background refetch must not blank the screen",
            vm.state.value is OrderDetailUiState.Loaded,
        )
        assertEquals("original", loadedOrder(vm).notes)

        gate.complete(ApiResult.Success(order(5, notes = "updated")))
        advanceUntilIdle()
        assertEquals("updated", loadedOrder(vm).notes)
    }

    @Test
    fun `a failed refetch keeps the loaded order instead of dropping to Error`() = runTest {
        var calls = 0
        coEvery { repository.getById(orderId) } coAnswers {
            if (calls++ == 0) {
                ApiResult.Success(order(5, notes = "original"))
            } else {
                ApiResult.Error(ApiError.Server(statusCode = 500, message = "boom"))
            }
        }

        val vm = viewModel()
        advanceUntilIdle()

        orderEventBus.emit(OrderEvent(orderId = orderId, eventKey = "order.completed"))
        advanceUntilIdle()

        assertTrue(vm.state.value is OrderDetailUiState.Loaded)
        assertEquals("original", loadedOrder(vm).notes)
    }

    @Test
    fun `the first load still shows the spinner`() = runTest {
        val gate = CompletableDeferred<ApiResult<OrderDetailDto>>()
        coEvery { repository.getById(orderId) } coAnswers { gate.await() }

        val vm = viewModel()
        runCurrent()
        assertEquals(OrderDetailUiState.Loading, vm.state.value)

        gate.complete(ApiResult.Success(order(5)))
        advanceUntilIdle()
        assertTrue(vm.state.value is OrderDetailUiState.Loaded)
    }

    @Test
    fun `retrying from the Error state shows the spinner again`() = runTest {
        val gate = CompletableDeferred<ApiResult<OrderDetailDto>>()
        var calls = 0
        coEvery { repository.getById(orderId) } coAnswers {
            if (calls++ == 0) ApiResult.Error(ApiError.Network("offline")) else gate.await()
        }

        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(OrderDetailUiState.Error(canRetry = true), vm.state.value)

        // There is nothing on screen worth keeping here, so the retry must
        // still render a spinner rather than sit on a dead Error screen.
        vm.refresh()
        runCurrent()
        assertEquals(OrderDetailUiState.Loading, vm.state.value)

        gate.complete(ApiResult.Success(order(5)))
        advanceUntilIdle()
        assertTrue(vm.state.value is OrderDetailUiState.Loaded)
    }

    @Test
    fun `a missing nav arg is a fatal error and never fetches`() = runTest {
        val vm = viewModel(id = null)
        advanceUntilIdle()

        assertEquals(OrderDetailUiState.Error(canRetry = false), vm.state.value)
        coVerify(exactly = 0) { repository.getById(any()) }
    }

    // ── "Make this recurring" gate ──

    @Test
    fun `a resolved non-member loses the make-recurring shortcut`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(RecurringAuthoringGate.Upsell, vm.recurringAuthoring.value)
    }

    /**
     * The defect: nothing on this screen fetched membership, so the shortcut was
     * withheld from paid-up members whenever no other screen had warmed the cache.
     * An answer that has not landed must not cost them the affordance — the server
     * refuses an unentitled create with its own localized message.
     */
    @Test
    fun `an unresolved membership keeps the make-recurring shortcut`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))
        coEvery { membershipRepository.refresh() } returns ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(RecurringAuthoringGate.Allowed, vm.recurringAuthoring.value)
    }

    @Test
    fun `a member keeps the make-recurring shortcut`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))
        coEvery { membershipRepository.refresh() } coAnswers { membershipAnswer(hasMembership = true) }

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(RecurringAuthoringGate.Allowed, vm.recurringAuthoring.value)
    }

    @Test
    fun `the screen fetches membership itself rather than trusting another screen's cache`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))

        viewModel()
        advanceUntilIdle()

        coVerify(exactly = 1) { membershipRepository.refresh() }
    }

    @Test
    fun `a membership answer still inside its freshness window is not re-fetched`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))
        membershipAnswer(hasMembership = true)

        viewModel()
        advanceUntilIdle()

        coVerify(exactly = 0) { membershipRepository.refresh() }
    }
}
