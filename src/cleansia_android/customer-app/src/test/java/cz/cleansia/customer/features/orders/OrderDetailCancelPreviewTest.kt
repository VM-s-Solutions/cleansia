package cz.cleansia.customer.features.orders

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.notifications.OrderEventBus
import cz.cleansia.customer.core.orders.CancelOrderResponse
import cz.cleansia.customer.core.orders.CancellationFeePreviewDto
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.user.CodeDto
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.cancel
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The cancel sheet asks the server what cancelling costs. What matters here is
 * the shape of not-knowing: the sheet is never handed a number the server did
 * not send, and a preview that fails never stands between the customer and
 * cancelling.
 *
 * A cancellable order is Confirmed, which arms the detail poller — so this file
 * settles work with `runCurrent()` and never `advanceUntilIdle()`, which would
 * drain an infinite stream of scheduled ticks (see [OrderDetailViewModelTest]).
 */
@OptIn(ExperimentalCoroutinesApi::class)
class OrderDetailCancelPreviewTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: OrderRepository
    private lateinit var membershipRepository: MembershipRepository
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

        coEvery { repository.getById(orderId) } returns ApiResult.Success(
            OrderDetailDto(
                id = orderId,
                totalPrice = 1000.0,
                originalSubtotal = 1000.0,
                appliedDiscountSource = 0,
                orderStatus = CodeDto(type = "OrderStatus", name = "Confirmed", value = 2),
            ),
        )
        coEvery { repository.getCancellationPreview(any()) } returns
            ApiResult.Error(ApiError.Network("offline"))
    }

    private fun viewModel(id: String? = orderId) = OrderDetailViewModel(
        orderRepository = repository,
        snackbar = snackbar,
        appContext = appContext,
        savedStateHandle = SavedStateHandle(mapOf("orderId" to id)),
        membershipRepository = membershipRepository,
        orderEventBus = orderEventBus,
    )

    private fun preview(tier: Int = 3) = CancellationFeePreviewDto(
        orderId = orderId,
        tier = tier,
        feeRate = 0.25,
        feeAmount = 250.0,
        refundAmount = 750.0,
        totalPrice = 1000.0,
        currencyCode = "CZK",
        expressWaiverForfeitedOnCancel = false,
    )

    @Test
    fun `the preview is only fetched when the sheet asks for it`() = runTest {
        val vm = viewModel()
        runCurrent()

        coVerify(exactly = 0) { repository.getCancellationPreview(any()) }

        vm.loadCancellationPreview()
        runCurrent()

        coVerify(exactly = 1) { repository.getCancellationPreview(orderId) }
        vm.viewModelScope.cancel()
    }

    @Test
    fun `a successful preview lands as Loaded carrying the server numbers`() = runTest {
        coEvery { repository.getCancellationPreview(orderId) } returns ApiResult.Success(preview())

        val vm = viewModel()
        runCurrent()
        vm.loadCancellationPreview()
        runCurrent()

        val state = vm.cancellationPreview.value
        assertTrue(state is CancellationPreviewUiState.Loaded)
        assertEquals(250.0, (state as CancellationPreviewUiState.Loaded).preview.feeAmount, 0.0)
        assertEquals(750.0, state.preview.refundAmount, 0.0)
        assertEquals(3, state.preview.tier)
        vm.viewModelScope.cancel()
    }

    @Test
    fun `the sheet shows a spinner rather than a stale number while the preview is in flight`() = runTest {
        val gate = CompletableDeferred<ApiResult<CancellationFeePreviewDto>>()
        coEvery { repository.getCancellationPreview(orderId) } coAnswers { gate.await() }

        val vm = viewModel()
        runCurrent()
        vm.loadCancellationPreview()
        runCurrent()

        assertEquals(CancellationPreviewUiState.Loading, vm.cancellationPreview.value)

        gate.complete(ApiResult.Success(preview()))
        runCurrent()
        assertTrue(vm.cancellationPreview.value is CancellationPreviewUiState.Loaded)
        vm.viewModelScope.cancel()
    }

    @Test
    fun `reopening the sheet re-asks instead of replaying the previous answer`() = runTest {
        coEvery { repository.getCancellationPreview(orderId) } returns ApiResult.Success(preview())

        val vm = viewModel()
        runCurrent()
        vm.loadCancellationPreview()
        runCurrent()
        assertTrue(vm.cancellationPreview.value is CancellationPreviewUiState.Loaded)

        // A quote goes stale by sitting still — the tier boundary moves with the
        // clock, so a second open must not render the first open's answer.
        vm.loadCancellationPreview()
        assertEquals(CancellationPreviewUiState.Loading, vm.cancellationPreview.value)

        runCurrent()
        coVerify(exactly = 2) { repository.getCancellationPreview(orderId) }
        vm.viewModelScope.cancel()
    }

    @Test
    fun `a failed preview is Error and is not also thrown at the snackbar`() = runTest {
        coEvery { repository.getCancellationPreview(orderId) } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "boom"))

        val vm = viewModel()
        runCurrent()
        vm.loadCancellationPreview()
        runCurrent()

        assertEquals(CancellationPreviewUiState.Error, vm.cancellationPreview.value)
        // The fee card carries the failure; a snackbar on top of it says the
        // same thing twice over the sheet that is already saying it.
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        vm.viewModelScope.cancel()
    }

    @Test
    fun `a preview outage never blocks the cancellation itself`() = runTest {
        coEvery { repository.getCancellationPreview(orderId) } returns
            ApiResult.Error(ApiError.Network("offline"))
        coEvery { repository.cancel(orderId, any()) } returns ApiResult.Success(
            CancelOrderResponse(
                orderId = orderId,
                feeRate = 0.0,
                refundAmount = 0.0,
                totalPrice = 1000.0,
                refundInitiated = false,
            ),
        )

        val vm = viewModel()
        runCurrent()
        vm.loadCancellationPreview()
        runCurrent()
        assertEquals(CancellationPreviewUiState.Error, vm.cancellationPreview.value)

        vm.cancel("no_longer_needed")
        runCurrent()

        coVerify(exactly = 1) { repository.cancel(orderId, "no_longer_needed") }
        vm.viewModelScope.cancel()
    }

    @Test
    fun `a missing nav arg errors without calling the server`() = runTest {
        val vm = viewModel(id = null)
        runCurrent()

        vm.loadCancellationPreview()
        runCurrent()

        assertEquals(CancellationPreviewUiState.Error, vm.cancellationPreview.value)
        coVerify(exactly = 0) { repository.getCancellationPreview(any()) }
        vm.viewModelScope.cancel()
    }
}
