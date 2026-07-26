package cz.cleansia.partner.features.orders

import cz.cleansia.core.location.LocationService
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.PagedDataOfOrderListItem
import cz.cleansia.partner.core.auth.EmployeeIdResolver
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.orders.OrdersMutation
import cz.cleansia.partner.data.orders.OrdersPane
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * Covers the inline row actions only — the tab/sort/period plumbing is
 * untested legacy and deliberately left that way here.
 *
 * Two stubs carry the whole fixture and must not be relaxed away:
 *  - `isPaneStale` returns **false** so the VM's `init` skips its background
 *    fetch. Every `getPaged` counted below therefore belongs to the action
 *    under test, not to start-up noise.
 *  - `getPaged` must be stubbed explicitly even though the repository mock is
 *    `relaxUnitFun`: a relaxed mock would hand back a mocked [ApiResult] that
 *    is neither `Success` nor `Error`, and the VM's exhaustive `when` would
 *    blow up with `NoWhenBranchMatchedException`.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class OrdersListViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var ordersRepository: OrdersRepository
    private lateinit var employeeIdResolver: EmployeeIdResolver
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController
    private lateinit var locationService: LocationService

    private val orderId = "order-1"

    @Before
    fun setUp() {
        ordersRepository = mockk(relaxUnitFun = true)
        employeeIdResolver = mockk()
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        locationService = mockk()

        every { errorTranslator.translate(any()) } returns "translated error"
        every { locationService.hasPermission() } returns false
        coEvery { locationService.getCurrentLocation() } returns null
        coEvery { employeeIdResolver.resolve() } returns "employee-1"
        every { ordersRepository.isPaneStale(any()) } returns false
        coEvery {
            ordersRepository.getPaged(any(), any(), any(), any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(PagedDataOfOrderListItem(data = emptyList()))
    }

    private fun viewModel() = OrdersListViewModel(
        ordersRepository,
        employeeIdResolver,
        errorTranslator,
        snackbar,
        locationService,
    )

    /**
     * The reject case has to reconcile exactly like the success case does. If
     * it does not, the row keeps its slider armed for an action the server has
     * already refused — the classic "someone else took this job" loop where the
     * cleaner swipes, gets an error, and swipes again on a row that should have
     * disappeared.
     */
    @Test
    fun `a rejected inline action invalidates the affected panes and refetches`() = runTest {
        coEvery { ordersRepository.takeOrder(orderId) } returns
            ApiResult.Error(ApiError.BadRequest("taken", errorKey = "order.already_taken"))

        val vm = viewModel()
        advanceUntilIdle()
        coVerify(exactly = 0) {
            ordersRepository.getPaged(any(), any(), any(), any(), any(), any(), any(), any(), any(), any())
        }

        vm.takeOrderInline(orderId)
        advanceUntilIdle()

        verify(exactly = 1) { ordersRepository.invalidatePanesFor(OrdersMutation.TakeOrder) }
        coVerify(exactly = 1) {
            ordersRepository.getPaged(any(), any(), any(), any(), any(), any(), any(), any(), any(), OrdersPane.Available)
        }
        assertNull(vm.uiState.value.inFlightActionOrderId)
    }

    /**
     * Same rule as the detail screen: one failed action means one snackbar. The
     * partner app wires no `NetworkErrorInterceptor`, so if the reconciling
     * fetch reported its own failure the cleaner would get the identical toast
     * twice for a single swipe.
     */
    @Test
    fun `a failed reconciling refetch raises only the action error`() = runTest {
        coEvery { ordersRepository.startOrder(orderId) } returns ApiResult.Error(ApiError.Network("down"))
        coEvery {
            ordersRepository.getPaged(any(), any(), any(), any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.startOrderInline(orderId)
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError("translated error") }
        coVerify(exactly = 1) {
            ordersRepository.getPaged(any(), any(), any(), any(), any(), any(), any(), any(), any(), any())
        }
    }
}
