package cz.cleansia.customer.features.orders

import android.content.Context
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.CancelOrderResponse
import cz.cleansia.customer.core.orders.CancellationFeePreviewDto
import cz.cleansia.customer.core.orders.GuestOrderDto
import cz.cleansia.customer.core.orders.GuestOrderRepository
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.withContext
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class GuestOrderViewModelTest {
    @get:Rule val mainRule = MainDispatcherRule()
    private val repository = mockk<GuestOrderRepository>()
    private val settings = mockk<AppSettingsRepository>()
    private val context = mockk<Context>()
    private lateinit var vm: GuestOrderViewModel
    private val order = GuestOrderDto("o-1", "CZ-123", null, 90.0, "EUR", 2)
    private val quote = CancellationFeePreviewDto("o-1", 3, 0.25, 22.5, 67.5, 90.0, "EUR", false)
    private val receipt = CancelOrderResponse("o-1", 0.25, 67.5, 90.0, true, 12.0)

    @Before
    fun setup() {
        every { context.getString(any()) } answers { "string:" + firstArg<Int>() }
        every { context.getString(R.string.order_cancel_success_with_refund, any()) } returns "issued refund"
        coEvery { settings.emailLanguageTag() } returns "sk"
        coEvery { repository.lookup(any()) } returns ApiResult.Success(order)
        coEvery { repository.preview(any()) } returns ApiResult.Success(quote)
        coEvery { repository.cancel(any(), any(), any()) } returns ApiResult.Success(receipt)
        vm = GuestOrderViewModel(repository, settings, context)
    }

    @Test
    fun `blank input never calls transport and a pasted link is reduced to its token`() = runTest {
        vm.lookup("  ")
        assertTrue(vm.state.value is GuestOrderUiState.Error)
        coVerify(exactly = 0) { repository.lookup(any()) }
        vm.lookup(" https://cleansia.cz/track-order?orderNumber=CZ-123&token=tok-1 ")
        assertEquals(GuestOrderUiState.Loading, vm.state.value)
        advanceUntilIdle()
        assertEquals(GuestOrderUiState.Loaded(order), vm.state.value)
        coVerify(exactly = 1) { repository.lookup("tok-1") }
    }

    @Test
    fun `editing the link clears the old order and a late lookup cannot restore it`() = runTest {
        val pending = CompletableDeferred<ApiResult<GuestOrderDto>>()
        coEvery { repository.lookup("old") } coAnswers {
            withContext(NonCancellable) { pending.await() }
        }
        vm.lookup("old")
        runCurrent()
        vm.onLinkChanged()
        assertEquals(GuestOrderUiState.Empty, vm.state.value)
        vm.lookup("new")
        runCurrent()
        pending.complete(ApiResult.Success(order.copy(id = "stale")))
        advanceUntilIdle()
        assertEquals(GuestOrderUiState.Loaded(order), vm.state.value)
    }

    @Test
    fun `lookup failure replaces previous details and is retryable`() = runTest {
        vm.lookup("tok-1")
        advanceUntilIdle()
        vm.onLinkChanged()
        coEvery { repository.lookup(any()) } returns ApiResult.Error(ApiError.NotFound("Booking not found"))
        vm.lookup("wrong")
        advanceUntilIdle()
        assertEquals(GuestOrderUiState.Error("Booking not found"), vm.state.value)
        coEvery { repository.lookup(any()) } returns ApiResult.Success(order)
        vm.lookup("tok-1")
        advanceUntilIdle()
        assertTrue(vm.state.value is GuestOrderUiState.Loaded)
    }

    @Test
    fun `failed unknown and mismatched previews cannot submit cancellation`() = runTest {
        vm.lookup("tok-1")
        advanceUntilIdle()
        val results = listOf(
            ApiResult.Error(ApiError.NotFound("Booking not found")),
            ApiResult.Success(quote.copy(tier = 99)),
            ApiResult.Success(quote.copy(orderId = "other")),
            ApiResult.Success(quote.copy(currencyCode = "CZK")),
        )
        for (result in results) {
            coEvery { repository.preview(any()) } returns result
            vm.openCancellation()
            advanceUntilIdle()
            assertEquals(CancellationPreviewUiState.Error, vm.preview.value)
            vm.cancel("schedule_changed")
        }
        advanceUntilIdle()
        coVerify(exactly = 0) { repository.cancel(any(), any(), any()) }
        coEvery { repository.preview(any()) } returns ApiResult.Success(quote)
        vm.loadPreview()
        advanceUntilIdle()
        assertEquals(CancellationPreviewUiState.Loaded(quote), vm.preview.value)
    }

    @Test
    fun `late preview after editing or dismissing cannot expose a fee`() = runTest {
        val pending = CompletableDeferred<ApiResult<CancellationFeePreviewDto>>()
        coEvery { repository.preview(any()) } coAnswers {
            withContext(NonCancellable) { pending.await() }
        }
        vm.lookup("tok-1")
        runCurrent()
        vm.openCancellation()
        runCurrent()
        vm.dismissCancellation()
        vm.onLinkChanged()
        pending.complete(ApiResult.Success(quote))
        advanceUntilIdle()
        assertEquals(GuestOrderUiState.Empty, vm.state.value)
        assertFalse(vm.showCancellation.value)
        assertEquals(CancellationPreviewUiState.Loading, vm.preview.value)
        vm.cancel("schedule_changed")
        coVerify(exactly = 0) { repository.cancel(any(), any(), any()) }
    }

    @Test
    fun `submit is guarded synchronously and uses the looked-up token plus selected language`() = runTest {
        vm.lookup(" tok-1 ")
        advanceUntilIdle()
        vm.openCancellation()
        advanceUntilIdle()
        vm.cancel("schedule_changed")
        vm.cancel("schedule_changed")
        assertEquals(ActionState.Submitting, vm.cancelState.value)
        vm.dismissCancellation()
        assertTrue(vm.showCancellation.value)
        advanceUntilIdle()
        coVerify(exactly = 1) { repository.cancel("tok-1", "schedule_changed", "sk") }
        assertEquals(GuestOrderUiState.Cancelled("issued refund"), vm.state.value)
        coVerify(exactly = 0) { repository.lookup("") }
    }

    @Test
    fun `success formats actual refund in order currency and never policy refund`() = runTest {
        vm.lookup("tok-1")
        advanceUntilIdle()
        vm.openCancellation()
        advanceUntilIdle()
        vm.cancel("schedule_changed")
        advanceUntilIdle()
        io.mockk.verify {
            context.getString(
                R.string.order_cancel_success_with_refund,
                cz.cleansia.core.format.formatOrderPrice(12.0, "EUR"),
            )
        }
    }

    @Test
    fun `null or zero actual refund uses success copy without a made up amount`() = runTest {
        for (amount in listOf(null, 0.0)) {
            coEvery { repository.cancel(any(), any(), any()) } returns
                ApiResult.Success(receipt.copy(actualRefundAmount = amount))
            vm.lookup("tok-1")
            advanceUntilIdle()
            vm.openCancellation()
            advanceUntilIdle()
            vm.cancel("schedule_changed")
            advanceUntilIdle()
            assertEquals(
                GuestOrderUiState.Cancelled("string:" + R.string.order_cancel_success_no_refund),
                vm.state.value,
            )
        }
        io.mockk.verify(exactly = 0) { context.getString(R.string.order_cancel_success_with_refund, any()) }
    }

    @Test
    fun `failed cancellation retains error and requires a fresh quote before retry`() = runTest {
        coEvery { repository.cancel(any(), any(), any()) } returns
            ApiResult.Error(ApiError.BadRequest("Unable to cancel"))
        vm.lookup("tok-1")
        advanceUntilIdle()
        vm.openCancellation()
        advanceUntilIdle()
        vm.cancel("schedule_changed")
        advanceUntilIdle()
        assertEquals(ActionState.Error("Unable to cancel"), vm.cancelState.value)
        assertTrue(vm.showCancellation.value)
        assertTrue(vm.state.value is GuestOrderUiState.Loaded)
        vm.cancel("schedule_changed")
        advanceUntilIdle()
        coVerify(exactly = 1) { repository.cancel(any(), any(), any()) }
    }

    @Test
    fun `reason limit and missing quote are enforced beyond the button`() = runTest {
        vm.lookup("tok-1")
        advanceUntilIdle()
        vm.cancel("schedule_changed")
        vm.openCancellation()
        vm.cancel("schedule_changed")
        advanceUntilIdle()
        vm.cancel("x".repeat(501))
        vm.cancel(" ")
        advanceUntilIdle()
        coVerify(exactly = 0) { repository.cancel(any(), any(), any()) }
        vm.cancel("x".repeat(500))
        advanceUntilIdle()
        coVerify(exactly = 1) { repository.cancel(any(), any(), any()) }
    }

    @Test
    fun `leaving the screen prevents a late cancellation response from exposing the previous order`() = runTest {
        val pending = CompletableDeferred<ApiResult<CancelOrderResponse>>()
        coEvery { repository.cancel(any(), any(), any()) } coAnswers {
            withContext(NonCancellable) { pending.await() }
        }
        vm.lookup("tok-1")
        runCurrent()
        vm.openCancellation()
        runCurrent()
        vm.cancel("schedule_changed")
        runCurrent()
        vm.clear()
        pending.complete(ApiResult.Success(receipt))
        advanceUntilIdle()
        assertEquals(GuestOrderUiState.Empty, vm.state.value)
        assertFalse(vm.showCancellation.value)
    }

    @Test
    fun `terminal and unknown orders never open cancellation`() = runTest {
        for (status in listOf(4, 5, 6, 99)) {
            coEvery { repository.lookup(any()) } returns ApiResult.Success(order.copy(status = status))
            vm.lookup("tok-1")
            advanceUntilIdle()
            vm.openCancellation()
            assertFalse(vm.showCancellation.value)
        }
        coVerify(exactly = 0) { repository.preview(any()) }
    }
}
