package cz.cleansia.customer.features.orders

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.notifications.OrderEventBus
import cz.cleansia.customer.core.orders.CancelOrderResponse
import cz.cleansia.customer.core.orders.OrderCurrencyDetailDto
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.user.CodeDto
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import java.io.File
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The signed-in cancel affordance follows the server's own set — the statuses
 * `CancellationAssessor.BlockedReason` does not refuse — and the figure it confirms is the refund
 * the server actually issued, not the policy figure the preview quoted.
 *
 * A cancellable order arms the detail poller, so this file settles work with `runCurrent()` and
 * cancels the scope at the end of each test (see [OrderDetailViewModelTest]).
 */
@OptIn(ExperimentalCoroutinesApi::class)
class OrderDetailCancelGateTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: OrderRepository
    private lateinit var membershipRepository: MembershipRepository
    private val markets = MutableStateFlow<MarketState>(MarketState.Unavailable)
    private val marketRepository = mockk<MarketRepository> {
        every { state } returns markets
        coEvery { ensureLoaded() } answers { markets.value }
    }
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context
    private lateinit var orderEventBus: OrderEventBus

    private val orderId = "order-1"

    @Before
    fun setUp() {
        repository = mockk(relaxed = true)
        membershipRepository = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        orderEventBus = OrderEventBus()

        every { appContext.getString(R.string.order_cancel_success_no_refund) } returns "cancelled"
        every { appContext.getString(R.string.order_cancel_success_with_refund, any()) } returns "refund issued"
    }

    private fun viewModel(id: String? = orderId) = OrderDetailViewModel(
        orderRepository = repository,
        marketRepository = marketRepository,
        snackbar = snackbar,
        appContext = appContext,
        savedStateHandle = SavedStateHandle(mapOf("orderId" to id)),
        membershipRepository = membershipRepository,
        orderEventBus = orderEventBus,
    )

    /** Wire values: New=0, Pending=1, Confirmed=2, OnTheWay=3, InProgress=4, Completed=5, Cancelled=6. */
    private fun stubOrder(statusValue: Int) {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(
            OrderDetailDto(
                id = orderId,
                totalPrice = 1000.0,
                originalSubtotal = 1000.0,
                appliedDiscountSource = 0,
                orderStatus = CodeDto(type = "OrderStatus", name = "status-$statusValue", value = statusValue),
                currency = OrderCurrencyDetailDto(code = "CZK"),
            ),
        )
    }

    private fun receipt(refundInitiated: Boolean, actualRefundAmount: Double?) = CancelOrderResponse(
        orderId = orderId,
        feeRate = 0.25,
        refundAmount = 750.0,
        totalPrice = 1000.0,
        refundInitiated = refundInitiated,
        actualRefundAmount = actualRefundAmount,
    )

    private fun TestScope.canCancelWhen(statusValue: Int): Boolean {
        stubOrder(statusValue)
        val vm = viewModel()
        runCurrent()
        return vm.canCancel.value.also { vm.viewModelScope.cancel() }
    }

    // ── status gating ──

    @Test
    fun `the affordance is offered in every status the server allows`() = runTest {
        assertTrue(canCancelWhen(0))
        assertTrue(canCancelWhen(1))
        assertTrue(canCancelWhen(2))
        assertTrue(canCancelWhen(3))
    }

    @Test
    fun `the affordance is withheld once work has started or the order is closed`() = runTest {
        assertFalse(canCancelWhen(4))
        assertFalse(canCancelWhen(5))
        assertFalse(canCancelWhen(6))
        assertFalse(canCancelWhen(99))
    }

    @Test
    fun `nothing is cancellable before the order has loaded`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Error(ApiError.Network("offline"))
        val vm = viewModel()
        runCurrent()

        assertFalse(vm.canCancel.value)
        vm.viewModelScope.cancel()
    }

    /** The one function both the signed-in and the guest surface read, so the two cannot drift. */
    @Test
    fun `the shared gate is the server's set`() {
        assertEquals(listOf(0, 1, 2, 3), (0..6).filter { customerCanCancelOrder(it) })
        assertFalse(customerCanCancelOrder(99))
        assertFalse(customerCanCancelOrder(null))
    }

    /**
     * The gate is a pure function the screen binds through the ViewModel. A status list re-inlined
     * in the composable would compile, and every test above would stay green while the two flows
     * drifted apart again — so the binding is pinned by reading the one line.
     */
    @Test
    fun `the screen reads the gate from the view model rather than a status list of its own`() {
        assertTrue(
            "OrderDetailScreen must bind isCancellable to viewModel.canCancel",
            screenSource.contains("val isCancellable by viewModel.canCancel.collectAsStateWithLifecycle()"),
        )
    }

    // ── the confirmed figure ──

    @Test
    fun `the success message carries the refund the server issued and never the policy figure`() = runTest {
        stubOrder(2)
        coEvery { repository.cancel(orderId, any()) } returns ApiResult.Success(
            receipt(refundInitiated = true, actualRefundAmount = 12.0),
        )
        val vm = viewModel()
        runCurrent()

        vm.cancel("schedule_changed")
        runCurrent()

        verify(exactly = 1) {
            appContext.getString(R.string.order_cancel_success_with_refund, formatOrderPrice(12.0, "CZK"))
        }
        verify(exactly = 0) {
            appContext.getString(R.string.order_cancel_success_with_refund, formatOrderPrice(750.0, "CZK"))
        }
        verify(exactly = 1) { snackbar.showSuccess("refund issued") }
        vm.viewModelScope.cancel()
    }

    @Test
    fun `a cancel that issued no refund uses the plain copy even when the policy quoted one`() = runTest {
        stubOrder(2)
        val vm = viewModel()
        runCurrent()

        listOf(
            receipt(refundInitiated = false, actualRefundAmount = null),
            receipt(refundInitiated = true, actualRefundAmount = null),
            receipt(refundInitiated = true, actualRefundAmount = 0.0),
        ).forEach { answer ->
            coEvery { repository.cancel(orderId, any()) } returns ApiResult.Success(answer)
            vm.cancel("schedule_changed")
            runCurrent()
        }

        verify(exactly = 3) { snackbar.showSuccess("cancelled") }
        verify(exactly = 0) { appContext.getString(R.string.order_cancel_success_with_refund, any()) }
        vm.viewModelScope.cancel()
    }

    // ── the reason cap ──

    @Test
    fun `the cap is the server validator's figure`() {
        assertEquals(500, CANCEL_REASON_MAX_LENGTH)
    }

    @Test
    fun `the notes limit leaves room for the reason code and its separator`() {
        assertEquals(500 - "no_longer_needed".length - 2, cancelNotesLimit("no_longer_needed"))
        assertEquals(500, cancelNotesLimit(null))
        assertEquals(0, cancelNotesLimit("x".repeat(600)))
    }

    private val screenSource: String = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer/features/orders/OrderDetailScreen.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("OrderDetailScreen.kt not found from working dir ${File(".").absolutePath}")
}
