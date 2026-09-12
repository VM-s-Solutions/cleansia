package cz.cleansia.customer.features.orders

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.orders.OrderListItemDto
import cz.cleansia.customer.core.orders.OrderRepository
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
class OrdersTabViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var orderRepository: OrderRepository
    private lateinit var snackbar: SnackbarController

    private val orders = MutableStateFlow<List<OrderListItemDto>>(emptyList())
    private val loading = MutableStateFlow(false)
    private val loadingMore = MutableStateFlow(false)
    private val loaded = MutableStateFlow(false)
    private val totalRecords = MutableStateFlow(0)

    @Before
    fun setUp() {
        orderRepository = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        every { orderRepository.orders } returns orders
        every { orderRepository.loading } returns loading
        every { orderRepository.loadingMore } returns loadingMore
        every { orderRepository.loaded } returns loaded
        every { orderRepository.totalRecords } returns totalRecords
    }

    private fun viewModel() = OrdersTabViewModel(orderRepository, snackbar)

    @Test
    fun `the exposed flows mirror the repository`() = runTest {
        val vm = viewModel()

        val order = mockk<OrderListItemDto>()
        orders.value = listOf(order)
        loading.value = true
        loadingMore.value = true
        loaded.value = true
        totalRecords.value = 7

        assertEquals(listOf(order), vm.orders.value)
        assertEquals(true, vm.loading.value)
        assertEquals(true, vm.loadingMore.value)
        assertEquals(true, vm.loaded.value)
        assertEquals(7, vm.totalRecords.value)
    }

    @Test
    fun `refresh snackbars a server failure and stays silent on a transport one`() = runTest {
        coEvery { orderRepository.refresh() } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "server boom"))
        val vm = viewModel()
        vm.refresh()
        advanceUntilIdle()
        verify(exactly = 1) { snackbar.showError(match<ApiError> { it.getUserMessage() == "server boom" }) }

        coEvery { orderRepository.refresh() } returns ApiResult.Error(ApiError.Network("offline"))
        vm.refresh()
        advanceUntilIdle()
        verify(exactly = 1) { snackbar.showError(any<ApiError>()) }
    }

    @Test
    fun `tab entry does not stack a refresh on one already in flight`() = runTest {
        coEvery { orderRepository.refresh() } returns ApiResult.Success(Unit)
        val vm = viewModel()

        loading.value = true
        vm.refreshUnlessLoading()
        advanceUntilIdle()
        coVerify(exactly = 0) { orderRepository.refresh() }

        loading.value = false
        vm.refreshUnlessLoading()
        advanceUntilIdle()
        coVerify(exactly = 1) { orderRepository.refresh() }
    }

    @Test
    fun `loadNextPage delegates to the repository`() = runTest {
        val vm = viewModel()
        vm.loadNextPage()
        advanceUntilIdle()
        coVerify(exactly = 1) { orderRepository.loadNextPage() }
    }
}
