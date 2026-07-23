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
