package cz.cleansia.partner.features.orders

import androidx.lifecycle.SavedStateHandle
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.data.orders.PendingOffer
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.testing.MainDispatcherRule
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
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The `order.preferred_offer` push still deep-links to the order detail, because the push fires on a
 * wider predicate than the reservation does — a short-lead recipient and a card order whose money has
 * not landed both get the push with no pending offer behind them, and a link to the offers list would
 * land them on an empty screen. So the detail is where the disclosure and the decline have to live.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class OrderDetailPreferredOfferTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var ordersRepository: OrdersRepository
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController
    private lateinit var offers: MutableStateFlow<List<PendingOffer>>

    private val orderId = "order-1"

    @Before
    fun setUp() {
        ordersRepository = mockk(relaxed = true)
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        offers = MutableStateFlow(emptyList())
        every { ordersRepository.pendingOffers } returns offers
        every { ordersRepository.arePendingOffersStale() } returns false
        every { ordersRepository.isOrderStale(orderId) } returns true
        coEvery { ordersRepository.getById(orderId) } returns ApiResult.Success(mockk<OrderItem>(relaxed = true))
        every { errorTranslator.translate(any()) } returns "translated error"
    }

    private fun viewModel() =
        OrderDetailViewModel(SavedStateHandle(mapOf("orderId" to orderId)), ordersRepository, errorTranslator, snackbar)

    private fun offer(id: String) = PendingOffer(
        id = id,
        displayOrderNumber = "CL-$id",
        cleaningDateTime = "2026-08-12T09:00:00Z",
        estimatedTime = 120,
        respondByUtc = "2026-08-10T18:40:00Z",
        customerAddressApproximate = "Praha 4 · 14000",
        rooms = 2,
        bathrooms = 1,
        totalPrice = 1200.0,
        currencyCode = "CZK",
    )

    @Test
    fun `the detail discloses a reservation held for this order alone`() = runTest {
        offers.value = listOf(offer("other-order"), offer(orderId))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals("2026-08-10T18:40:00Z", vm.preferredOffer.value?.respondByUtc)
    }

    @Test
    fun `an order nobody reserved discloses nothing`() = runTest {
        offers.value = listOf(offer("other-order"))

        val vm = viewModel()
        advanceUntilIdle()

        assertNull(vm.preferredOffer.value)
    }

    /**
     * The 2-8 hour band pushes a named cleaner without withholding a seat, and a card order is pushed
     * before its money lands. Both reach this screen with no reservation behind them, and the screen
     * has to be an ordinary job in that case rather than claim one.
     */
    @Test
    fun `a stale offers cache is filled so the disclosure is not silently absent`() = runTest {
        every { ordersRepository.arePendingOffersStale() } returns true
        coEvery { ordersRepository.refreshPendingOffers() } answers {
            offers.value = listOf(offer(orderId))
            ApiResult.Success(offers.value)
        }

        val vm = viewModel()
        advanceUntilIdle()

        coVerify(exactly = 1) { ordersRepository.refreshPendingOffers() }
        assertEquals(orderId, vm.preferredOffer.value?.id)
    }

    @Test
    fun `declining from the detail calls the decline endpoint and the disclosure goes`() = runTest {
        offers.value = listOf(offer(orderId))
        val vm = viewModel()
        advanceUntilIdle()
        coEvery { ordersRepository.declinePreferredOffer(orderId) } answers {
            offers.value = emptyList()
            ApiResult.Success(Unit)
        }

        vm.declinePreferredOffer()
        advanceUntilIdle()

        coVerify(exactly = 1) { ordersRepository.declinePreferredOffer(orderId) }
        assertNull(vm.preferredOffer.value)
    }

    /**
     * Nothing gates the reservation on the weekly cap, so the take gate can refuse a job the cleaner
     * was told was theirs. On a disclosed offer that refusal is framed by the screen as the platform's
     * mistake, so it must not also arrive as a bare snackbar line.
     */
    @Test
    fun `a refused confirm on a disclosed offer is state, not a bare snackbar`() = runTest {
        offers.value = listOf(offer(orderId))
        val vm = viewModel()
        advanceUntilIdle()
        every { errorTranslator.translate(any()) } returns "You've reached your weekly order limit."
        coEvery { ordersRepository.takeOrder(orderId) } returns ApiResult.Error(
            ApiError.BadRequest("nope", null, null, "order.weekly_limit_reached"),
        )

        vm.take()
        advanceUntilIdle()

        assertEquals(
            ActionState.Error("You've reached your weekly order limit."),
            vm.actionState.value,
        )
        assertEquals(OfferAction.Confirm, vm.offerRefusal.value?.action)
        assertEquals("You've reached your weekly order limit.", vm.offerRefusal.value?.reason)
        verify(exactly = 0) { snackbar.showError(any()) }
    }

    /**
     * The bug this pins: every failed action set the same error state and the screen inferred the
     * framing from "there is a live offer", so failing to RELEASE a job apologised for failing to hand
     * it over. Nothing was handed over — nothing changed at all — and the causes are transient.
     */
    @Test
    fun `a refused decline on a disclosed offer reads as a release failure`() = runTest {
        offers.value = listOf(offer(orderId))
        val vm = viewModel()
        advanceUntilIdle()
        coEvery { ordersRepository.declinePreferredOffer(orderId) } returns
            ApiResult.Error(ApiError.Network("down"))

        vm.declinePreferredOffer()
        advanceUntilIdle()

        assertEquals(OfferAction.Decline, vm.offerRefusal.value?.action)
        verify(exactly = 0) { snackbar.showError(any()) }
    }

    @Test
    fun `an action that succeeds clears the refusal left by the one before it`() = runTest {
        offers.value = listOf(offer(orderId))
        val vm = viewModel()
        advanceUntilIdle()
        coEvery { ordersRepository.takeOrder(orderId) } returns
            ApiResult.Error(ApiError.BadRequest("nope", null, null, "order.weekly_limit_reached"))
        vm.take()
        advanceUntilIdle()
        assertEquals(OfferAction.Confirm, vm.offerRefusal.value?.action)

        coEvery { ordersRepository.declinePreferredOffer(orderId) } answers {
            offers.value = emptyList()
            ApiResult.Success(Unit)
        }
        vm.declinePreferredOffer()
        advanceUntilIdle()

        assertNull(vm.offerRefusal.value)
    }

    @Test
    fun `dismissing the refusal clears it`() = runTest {
        offers.value = listOf(offer(orderId))
        val vm = viewModel()
        advanceUntilIdle()
        coEvery { ordersRepository.takeOrder(orderId) } returns
            ApiResult.Error(ApiError.BadRequest("nope", null, null, "order.weekly_limit_reached"))
        vm.take()
        advanceUntilIdle()

        vm.dismissOfferRefusal()

        assertNull(vm.offerRefusal.value)
    }

    @Test
    fun `a refused take on an ordinary job still reaches the snackbar`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        coEvery { ordersRepository.takeOrder(orderId) } returns ApiResult.Error(
            ApiError.BadRequest("nope", null, null, "order.no_available_spots"),
        )

        vm.take()
        advanceUntilIdle()

        verify { snackbar.showError("translated error") }
        assertNull("an ordinary job has no offer to apologise about", vm.offerRefusal.value)
    }
}
