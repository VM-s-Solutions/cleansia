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
import io.mockk.clearMocks
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * Covers the inline row actions and the start-up fetch — the sort/period plumbing is untested legacy
 * and deliberately left that way here.
 *
 * `isPaneStale` returns **false** throughout, which is the interesting case: it used to make the VM's
 * `init` skip its fetch entirely. Start-up calls are dropped with [clearMocks] rather than stubbed
 * away, so every `getPaged` counted in an action test still belongs to the action under test.
 *
 * `getPaged` must be stubbed explicitly even though the repository mock is `relaxUnitFun`: a relaxed
 * mock would hand back a mocked [ApiResult] that is neither `Success` nor `Error`, and the VM's
 * exhaustive `when` would blow up with `NoWhenBranchMatchedException`.
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
     * Forgets the start-up fetch's recorded calls while keeping every stub in place.
     *
     * The snackbar is cleared too: when a test stubs `getPaged` to fail, the start-up fetch fails with
     * it and reports that — correctly, but it is not the error the action test is counting.
     */
    private fun forgetStartUpCalls() =
        clearMocks(ordersRepository, snackbar, answers = false, childMocks = false)

    /**
     * THE REGRESSION. `ensureFreshOrCachedAsync` skipped the fetch whenever the pane's watermark was
     * fresh — but the repository caches watermarks, not rows, so nothing refilled the list. The screen
     * sat on `isInitialLoad` forever and the cleaner had to pull to refresh to see any job at all. It
     * reproduced on every route back into the tab inside the freshness window.
     */
    @Test
    fun `a fresh pane still loads on start-up rather than stranding the spinner`() = runTest {
        every { ordersRepository.isPaneStale(any()) } returns false

        val vm = viewModel()
        advanceUntilIdle()

        coVerify(exactly = 1) {
            ordersRepository.getPaged(any(), any(), any(), any(), any(), any(), any(), any(), any(), OrdersPane.Available)
        }
        assertFalse("the spinner never cleared", vm.uiState.value.isInitialLoad)
        assertTrue(vm.uiState.value.hasLoadedOnce)
    }

    /**
     * The other half: once the rows ARE on screen, a fresh pane must not round-trip again. That is the
     * whole point of the watermark, and the fix above must not have traded the stuck spinner for a
     * fetch on every resume.
     */
    @Test
    fun `a resume on a loaded fresh pane does not refetch`() = runTest {
        every { ordersRepository.isPaneStale(any()) } returns false

        val vm = viewModel()
        advanceUntilIdle()
        forgetStartUpCalls()

        vm.onResume()
        advanceUntilIdle()

        coVerify(exactly = 0) {
            ordersRepository.getPaged(any(), any(), any(), any(), any(), any(), any(), any(), any(), any())
        }
    }

    /**
     * `selectTab` empties the list before it asks for the new pane, so a fresh watermark on that pane
     * must not be allowed to suppress the fetch — the cleaner would land on an empty tab.
     */
    @Test
    fun `switching to a fresh pane fetches it rather than showing an empty list`() = runTest {
        every { ordersRepository.isPaneStale(any()) } returns false

        val vm = viewModel()
        advanceUntilIdle()
        forgetStartUpCalls()

        vm.selectTab(OrdersTab.MyActive)
        advanceUntilIdle()

        coVerify(exactly = 1) {
            ordersRepository.getPaged(any(), any(), any(), any(), any(), any(), any(), any(), any(), OrdersPane.Active)
        }
    }

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
        forgetStartUpCalls()

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
        forgetStartUpCalls()

        vm.startOrderInline(orderId)
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError("translated error") }
        coVerify(exactly = 1) {
            ordersRepository.getPaged(any(), any(), any(), any(), any(), any(), any(), any(), any(), any())
        }
    }
}
