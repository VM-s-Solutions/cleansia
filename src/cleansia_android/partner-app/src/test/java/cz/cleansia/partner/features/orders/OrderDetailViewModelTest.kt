package cz.cleansia.partner.features.orders

import androidx.lifecycle.SavedStateHandle
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.core.network.ApiError
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

    private val orderId = "order-1"
    private val order = mockk<OrderItem>()

    @Before
    fun setUp() {
        ordersRepository = mockk(relaxed = true)
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
    }

    private fun viewModel() =
        OrderDetailViewModel(SavedStateHandle(mapOf("orderId" to orderId)), ordersRepository, errorTranslator, snackbar)

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

    @Test
    fun `warm cache skips the network and stays Loading until refreshed`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns false

        val vm = viewModel()
        advanceUntilIdle()

        io.mockk.coVerify(exactly = 0) { ordersRepository.getById(orderId) }
        assertEquals(OrderDetailUiState.Loading, vm.uiState.value)
    }

    @Test
    fun `take action drives ActionState and inFlightAction then returns to Idle`() = runTest {
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(order)
        coEvery { ordersRepository.takeOrder(orderId) } returns ApiResult.Success(Unit)

        val vm = viewModel()
        advanceUntilIdle()

        vm.take()
        advanceUntilIdle()

        assertEquals(ActionState.Idle, vm.actionState.value)
        assertNull(vm.inFlightAction.value)
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
        val refreshed = mockk<OrderItem>()
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returnsMany listOf(
            ApiResult.Success(order),
            ApiResult.Success(refreshed),
        )
        coEvery { ordersRepository.takeOrder(orderId) } returns
            ApiResult.Error(ApiError.BadRequest("taken", errorKey = "order.already_taken"))

        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(OrderDetailUiState.Loaded(order), vm.uiState.value)

        vm.take()
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
        var takeCalls = 0
        coEvery { ordersRepository.takeOrder(orderId) } coAnswers {
            takeCalls++
            ApiResult.Success(Unit)
        }

        val vm = viewModel()
        advanceUntilIdle()

        vm.take()
        vm.take()
        advanceUntilIdle()

        assertEquals(1, takeCalls)
    }
}
