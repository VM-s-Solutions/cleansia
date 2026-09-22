package cz.cleansia.partner.features.orders

import androidx.lifecycle.SavedStateHandle
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.api.model.AssignedEmployeeDto
import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.WorkContractAcceptanceDto
import cz.cleansia.core.network.ApiError
import cz.cleansia.partner.core.auth.EmployeeIdResolver
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.features.orders.OrderAction
import cz.cleansia.partner.features.orders.OrderDetailUiState
import cz.cleansia.partner.features.orders.OrderDetailViewModel
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class OrderDetailViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var ordersRepository: OrdersRepository
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController
    private lateinit var employeeIdResolver: EmployeeIdResolver

    private val orderId = "order-1"

    /** A job the caller is not on: the standing resolver reads the crew and nothing else. */
    private fun crewless() = mockk<OrderItem> { every { assignedEmployees } returns null }

    private val order = crewless()

    @Before
    fun setUp() {
        ordersRepository = mockk(relaxed = true)
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        employeeIdResolver = mockk()
        every { errorTranslator.translate(any()) } returns "translated error"
        coEvery { employeeIdResolver.resolve() } returns "employee-me"
    }

    private fun viewModel() = OrderDetailViewModel(
        SavedStateHandle(mapOf("orderId" to orderId)),
        ordersRepository,
        errorTranslator,
        snackbar,
        employeeIdResolver,
    )

    private fun refusal(key: String) = ApiError.BadRequest(
        message = "A validation problem occurred.",
        validationErrors = mapOf("Command" to listOf(key)),
        errorKey = key,
    )

    private fun crewOrder(status: Int, mySeatId: String = "seat-me", acceptance: WorkContractAcceptanceDto? = null) = OrderItem(
        orderStatus = Code(value = status),
        isAssignedToCurrentUser = true,
        assignedEmployees = listOf(AssignedEmployeeDto(id = mySeatId, employeeId = "employee-me", fullName = "Me")),
        workContractAcceptances = listOfNotNull(acceptance),
    )

    @Test
    fun `cold init fetches and transitions Loading to Loaded`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(order)

        val vm = viewModel()
        assertEquals(OrderDetailUiState.Loading, vm.uiState.value)

        advanceUntilIdle()
        assertEquals(OrderDetailUiState.Loaded(order), vm.uiState.value)
    }

    @Test
    fun `init fetch failure with no order surfaces Error and snackbars`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        assertTrue(vm.uiState.value is OrderDetailUiState.Error)
        verify { snackbar.showError("translated error") }
    }

    /**
     * The contract the new Retry button on the Error state depends on.
     *
     * Retry is deliberately wired to [OrderDetailViewModel.refresh] and not to
     * `onResume`/`ensureFreshOrCachedAsync`, because those consult
     * [OrdersRepository.isOrderStale] and return without touching the network
     * when the cache is warm — a Retry button that silently does nothing. So
     * this test pins the warm-cache case specifically: `isOrderStale` is false
     * for the whole run, and `refresh()` must still re-fetch and recover.
     */
    @Test
    fun `refresh from the Error state re-fetches and recovers to Loaded`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returnsMany listOf(
            ApiResult.Error(ApiError.Network("down")),
            ApiResult.Success(order),
        )

        val vm = viewModel()
        advanceUntilIdle()
        assertTrue(vm.uiState.value is OrderDetailUiState.Error)

        // The order is no longer stale by the time the user taps Retry, which
        // is exactly the case that would defeat the staleness-gated path.
        every { ordersRepository.isOrderStale(orderId) } returns false

        vm.refresh()
        advanceUntilIdle()

        assertEquals(OrderDetailUiState.Loaded(order), vm.uiState.value)
        io.mockk.coVerify(exactly = 2) { ordersRepository.getById(orderId) }
    }

    /**
     * This used to assert the sheet STAYS on Loading — the defect written down as intent. The
     * repository stores a per-order WATERMARK, not the order, and the watermark outlives the view
     * model, so opening a job inside the freshness window left the sheet spinning with nothing on the
     * way and pull-to-refresh the only way out.
     */
    @Test
    fun `a warm watermark still loads a sheet that is holding nothing`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns false
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(order)

        val vm = viewModel()
        advanceUntilIdle()

        io.mockk.coVerify(exactly = 1) { ordersRepository.getById(orderId) }
        assertEquals(OrderDetailUiState.Loaded(order), vm.uiState.value)
    }

    /** The other half: once the order IS on screen, a warm watermark must still spare the round-trip. */
    @Test
    fun `a warm watermark skips the refetch once the sheet is loaded`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns false
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(order)

        val vm = viewModel()
        advanceUntilIdle()

        vm.onResume()
        advanceUntilIdle()

        io.mockk.coVerify(exactly = 1) { ordersRepository.getById(orderId) }
    }

    /** Taking is accepting the contract for work: the tap opens the sheet, the swipe inside it takes. */
    @Test
    fun `take opens the contract sheet for this order and takes nothing itself`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(order)

        val vm = viewModel()
        advanceUntilIdle()

        vm.take()
        advanceUntilIdle()

        assertEquals(WorkContractRequest.Take(orderId), vm.contractRequest.value)
        io.mockk.coVerify(exactly = 0) { ordersRepository.takeOrder(any(), any()) }
        assertEquals(ActionState.Idle, vm.actionState.value)
    }

    @Test
    fun `a Taken outcome closes the sheet, refetches the order and returns to Idle`() = runTest {
        val refreshed = crewless()
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returnsMany listOf(
            ApiResult.Success(order),
            ApiResult.Success(refreshed),
        )

        val vm = viewModel()
        advanceUntilIdle()
        vm.take()

        vm.onWorkContractOutcome(WorkContractOutcome.Taken(WorkContractRequest.Take(orderId)))
        advanceUntilIdle()

        assertNull(vm.contractRequest.value)
        assertEquals(OrderDetailUiState.Loaded(refreshed), vm.uiState.value)
        assertEquals(ActionState.Idle, vm.actionState.value)
        assertNull(vm.inFlightAction.value)
    }

    @Test
    fun `dismissing the sheet closes it without touching the order`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(order)

        val vm = viewModel()
        advanceUntilIdle()
        vm.take()

        vm.dismissContract()
        advanceUntilIdle()

        assertNull(vm.contractRequest.value)
        io.mockk.coVerify(exactly = 1) { ordersRepository.getById(orderId) }
    }

    /**
     * A seat an administrator placed has no acceptance, and the start is the first act the server
     * refuses for it. That refusal is not a message to read but the sheet to open, in accept mode,
     * with the start's own gesture released so it can be retried once the row exists.
     */
    @Test
    fun `a start refused for a missing acceptance opens the sheet in accept mode and snackbars nothing`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(order)
        coEvery { ordersRepository.startOrder(orderId) } returns ApiResult.Error(refusal("contract.acceptance_required"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.start()
        advanceUntilIdle()

        assertEquals(WorkContractRequest.Accept(orderId), vm.contractRequest.value)
        assertEquals(ActionState.Idle, vm.actionState.value)
        assertNull(vm.inFlightAction.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        verify(exactly = 0) { snackbar.showError(any<ApiError>()) }
    }

    @Test
    fun `a completion refused for a missing acceptance opens the same sheet`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(order)
        coEvery { ordersRepository.completeOrder(orderId, null, null) } returns
            ApiResult.Error(refusal("contract.acceptance_required"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.complete(null, null)
        advanceUntilIdle()

        assertEquals(WorkContractRequest.Accept(orderId), vm.contractRequest.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    /** Any other refusal of a start is still an error to read, exactly as before. */
    @Test
    fun `a start refused for another reason still snackbars and does not open the sheet`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(order)
        coEvery { ordersRepository.startOrder(orderId) } returns ApiResult.Error(refusal("order.too_early_to_start"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.start()
        advanceUntilIdle()

        assertNull(vm.contractRequest.value)
        verify(exactly = 1) { snackbar.showError("translated error") }
    }

    @Test
    fun `an Accepted outcome refetches so the line replaces the banner`() = runTest {
        val before = crewOrder(status = 2)
        val after = crewOrder(
            status = 2,
            acceptance = WorkContractAcceptanceDto(
                id = "acc-1",
                orderEmployeeId = "seat-me",
                employeeId = "employee-me",
                acceptedOn = "2026-08-10T18:40:00Z",
                documentVersion = "2026-09-20",
                language = "cs",
            ),
        )
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returnsMany listOf(ApiResult.Success(before), ApiResult.Success(after))

        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(WorkContractStanding.Pending, vm.contractStanding.value)
        vm.openContract(WorkContractRequest.Accept(orderId))

        vm.onWorkContractOutcome(WorkContractOutcome.Accepted(WorkContractRequest.Accept(orderId)))
        advanceUntilIdle()

        assertNull(vm.contractRequest.value)
        assertEquals(WorkContractStanding.Accepted("acc-1", "2026-08-10T18:40:00Z", "2026-09-20"), vm.contractStanding.value)
        assertEquals(ActionState.Idle, vm.actionState.value)
    }

    @Test
    fun `the standing waits for the employee id and pairs by the seat`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(crewOrder(status = 3))
        coEvery { employeeIdResolver.resolve() } returns null

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(WorkContractStanding.None, vm.contractStanding.value)
    }

    @Test
    fun `markCashCollected action calls repo and returns to Idle`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(order)
        coEvery { ordersRepository.markCashCollected(orderId) } returns ApiResult.Success(Unit)

        val vm = viewModel()
        advanceUntilIdle()

        vm.markCashCollected()
        advanceUntilIdle()

        io.mockk.coVerify { ordersRepository.markCashCollected(orderId) }
        assertEquals(ActionState.Idle, vm.actionState.value)
        assertNull(vm.inFlightAction.value)
    }

    @Test
    fun `action failure surfaces ActionState Error and snackbars`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(order)
        coEvery { ordersRepository.startOrder(orderId) } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.start()
        advanceUntilIdle()

        assertTrue(vm.actionState.value is ActionState.Error)
        assertNull(vm.inFlightAction.value)
        verify { snackbar.showError("translated error") }
    }

    /**
     * A clean server reject — another cleaner took the job, or the status moved
     * on from a different device — used to leave the footer offering the exact
     * action that had just been refused, because only the success branch
     * refetched. The reconciling fetch deliberately bypasses
     * [OrdersRepository.isOrderStale]: the local copy of the order is *known*
     * to disagree with the server, so a warm cache is precisely the wrong thing
     * to trust here.
     */
    @Test
    fun `a rejected action refetches so the footer cannot keep offering it`() = runTest {
        val refreshed = crewless()
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returnsMany listOf(
            ApiResult.Success(order),
            ApiResult.Success(refreshed),
        )
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(OrderDetailUiState.Loaded(order), vm.uiState.value)

        vm.onWorkContractOutcome(
            WorkContractOutcome.Refused(
                WorkContractRequest.Take(orderId),
                ApiError.BadRequest("taken", errorKey = "order.already_taken"),
            ),
        )
        advanceUntilIdle()

        assertEquals(OrderDetailUiState.Loaded(refreshed), vm.uiState.value)
        assertTrue(vm.actionState.value is ActionState.Error)
        assertNull(vm.inFlightAction.value)
    }

    /**
     * The reconciling fetch must not be able to turn one failure into two
     * snackbars. The partner app has no `NetworkErrorInterceptor` (only the
     * customer app wires one), so nothing above the ViewModel de-duplicates
     * toasts — the refetch has to stay silent about its own error, and the
     * order already on screen has to survive it.
     */
    @Test
    fun `a failed reconciling refetch keeps the order and raises only the action error`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returnsMany listOf(
            ApiResult.Success(order),
            ApiResult.Error(ApiError.Network("down")),
        )
        coEvery { ordersRepository.startOrder(orderId) } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.start()
        advanceUntilIdle()

        assertEquals(OrderDetailUiState.Loaded(order), vm.uiState.value)
        verify(exactly = 1) { snackbar.showError("translated error") }
        io.mockk.coVerify(exactly = 2) { ordersRepository.getById(orderId) }
    }

    @Test
    fun `action is re-entry guarded while submitting`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(order)
        var startCalls = 0
        coEvery { ordersRepository.startOrder(orderId) } coAnswers {
            startCalls++
            ApiResult.Success(Unit)
        }

        val vm = viewModel()
        advanceUntilIdle()

        vm.start()
        vm.start()
        advanceUntilIdle()

        assertEquals(1, startCalls)
    }
}
