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
import cz.cleansia.customer.core.notifications.OrderEvent
import cz.cleansia.customer.core.notifications.OrderEventBus
import cz.cleansia.customer.core.orders.AssignedEmployeeDto
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
import kotlinx.datetime.Clock
import kotlinx.datetime.Instant
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import kotlin.time.Duration.Companion.hours
import kotlin.time.Duration.Companion.seconds

/**
 * The signed-in cancel affordance follows the server's own set — the statuses
 * `CancellationAssessor.BlockedReason` does not refuse — and the figure it confirms is the refund
 * the server actually issued, not the policy figure the preview quoted.
 *
 * A Confirmed/OnTheWay order arms the detail poller, so this file settles work with `runCurrent()`
 * and cancels the scope at the end of each test (see [OrderDetailViewModelTest]).
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

    /**
     * Wire values: New=0, Pending=1, Confirmed=2, OnTheWay=3, InProgress=4, Completed=5, Cancelled=6;
     * PaymentType Cash=1, Card=2; PaymentStatus Pending=1, Paid=2, Failed=3.
     */
    private fun orderDto(
        statusValue: Int,
        staffed: Boolean = false,
        startsAt: Instant? = null,
        paymentType: Int? = null,
        paymentStatus: Int? = null,
    ) = OrderDetailDto(
        id = orderId,
        totalPrice = 1000.0,
        originalSubtotal = 1000.0,
        appliedDiscountSource = 0,
        orderStatus = CodeDto(type = "OrderStatus", name = "status-$statusValue", value = statusValue),
        currency = OrderCurrencyDetailDto(code = "CZK"),
        cleaningDateTime = startsAt?.toString(),
        assignedEmployees = if (staffed) listOf(AssignedEmployeeDto(id = "seat-1", employeeId = "emp-1")) else null,
        paymentType = paymentType?.let { CodeDto(type = "PaymentType", name = "type-$it", value = it) },
        paymentStatus = paymentStatus?.let { CodeDto(type = "PaymentStatus", name = "status-$it", value = it) },
    )

    private fun stubOrder(statusValue: Int) {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(orderDto(statusValue))
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

    // ── after the booked start ──

    /** (Cancel offered, "the cleaner did not arrive" offered) for one loaded order. */
    private fun TestScope.footerFor(order: OrderDetailDto): Pair<Boolean, Boolean> {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order)
        val vm = viewModel()
        runCurrent()
        return (vm.canCancel.value to vm.canReportCleanerNoShow.value).also { vm.viewModelScope.cancel() }
    }

    @Test
    fun `past the start with a cleaner on the job and nobody started, Cancel gives way to the report`() = runTest {
        val started = Clock.System.now() - 1.hours
        assertEquals(false to true, footerFor(orderDto(2, staffed = true, startsAt = started)))
        assertEquals(false to true, footerFor(orderDto(3, staffed = true, startsAt = started)))
    }

    @Test
    fun `before the start a staffed order keeps Cancel and offers no report`() = runTest {
        assertEquals(true to false, footerFor(orderDto(3, staffed = true, startsAt = Clock.System.now() + 2.hours)))
    }

    @Test
    fun `past the start with nobody on the job Cancel stays, as the server allows it`() = runTest {
        assertEquals(true to false, footerFor(orderDto(0, staffed = false, startsAt = Clock.System.now() - 1.hours)))
    }

    @Test
    fun `once work has started neither is offered`() = runTest {
        assertEquals(false to false, footerFor(orderDto(4, staffed = true, startsAt = Clock.System.now() - 1.hours)))
    }

    @Test
    fun `a refetch of the unchanged order re-reads the clock, so the report appears once the start passes`() =
        runTest {
            var startsAt: Instant? = null
            coEvery { repository.getById(orderId) } answers {
                val start = startsAt ?: (Clock.System.now() + 1.seconds).also { startsAt = it }
                ApiResult.Success(orderDto(2, staffed = true, startsAt = start))
            }
            val vm = viewModel()
            runCurrent()
            assertTrue(vm.canCancel.value)
            assertFalse(vm.canReportCleanerNoShow.value)

            Thread.sleep(1_100)
            orderEventBus.emit(OrderEvent(orderId = orderId, eventKey = "order.status_changed"))
            runCurrent()

            assertFalse(vm.canCancel.value)
            assertTrue(vm.canReportCleanerNoShow.value)
            vm.viewModelScope.cancel()
        }

    @Test
    fun `the report is offered from the booked start itself, where the server starts refusing`() {
        val start = Instant.parse("2026-10-01T09:00:00Z")
        assertTrue(customerAwaitsCleanerPastStart(2, hasCleaner = true, startsAt = start, now = start))
        assertFalse(customerAwaitsCleanerPastStart(2, hasCleaner = true, startsAt = start, now = start - 1.seconds))
        assertFalse(customerAwaitsCleanerPastStart(2, hasCleaner = true, startsAt = null, now = start))
        assertFalse(customerAwaitsCleanerPastStart(2, hasCleaner = false, startsAt = start, now = start))
        assertFalse(customerAwaitsCleanerPastStart(5, hasCleaner = true, startsAt = start, now = start))
    }

    // ── the refund the sheet may estimate ──

    private fun TestScope.tookNoCardPaymentFor(order: OrderDetailDto): Boolean {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order)
        val vm = viewModel()
        runCurrent()
        return vm.tookNoCardPayment.value.also { vm.viewModelScope.cancel() }
    }

    @Test
    fun `a cash booking or a card booking that took no payment has no card refund to estimate`() = runTest {
        assertTrue(tookNoCardPaymentFor(orderDto(2, paymentType = 1, paymentStatus = 1)))
        assertTrue(tookNoCardPaymentFor(orderDto(2, paymentType = 1, paymentStatus = 2)))
        assertTrue(tookNoCardPaymentFor(orderDto(2, paymentType = 2, paymentStatus = 1)))
        assertTrue(tookNoCardPaymentFor(orderDto(2, paymentType = 2, paymentStatus = 3)))
    }

    @Test
    fun `a charged card booking keeps its refund estimate`() = runTest {
        assertFalse(tookNoCardPaymentFor(orderDto(2, paymentType = 2, paymentStatus = 2)))
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

    /**
     * The sheet is the only place the limit is applied, and a private figure re-inlined there
     * would compile with both tests above still green — so its two clip sites are pinned the
     * same way the screen's gate binding is.
     */
    @Test
    fun `the sheet clips the notes through the shared limit and carries no figure of its own`() {
        val clipSites = Regex("""notes = \w+\.take\((\w+)\(""")
            .findAll(sheetSource)
            .map { it.groupValues[1] }
            .toList()
        assertEquals(listOf("cancelNotesLimit", "cancelNotesLimit"), clipSites)
        assertEquals(clipSites.size, Regex("""\.take\(""").findAll(sheetSource).count())
        assertFalse(sheetSource.contains("MAX_REASON_LENGTH"))
        assertFalse(Regex("""\b2000\b""").containsMatchIn(sheetSource))
    }

    private val screenSource: String = featureSource("OrderDetailScreen.kt")
    private val sheetSource: String = featureSource("CancelOrderSheet.kt")

    private fun featureSource(fileName: String): String = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer/features/orders/$fileName") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("$fileName not found from working dir ${File(".").absolutePath}")
}
