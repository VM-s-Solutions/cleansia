package cz.cleansia.partner.features.orders

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.orders.PendingOffer
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The entry point to the offers surface, and the reason there is no permanent tab: a cleaner with no
 * pending offer — which is nearly every cleaner on nearly every day — is shown nothing at all, so the
 * empty state is never a cost anyone pays daily.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class PendingOffersCardViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var ordersRepository: OrdersRepository
    private lateinit var offers: MutableStateFlow<List<PendingOffer>>

    @Before
    fun setUp() {
        ordersRepository = mockk(relaxed = true)
        offers = MutableStateFlow(emptyList())
        every { ordersRepository.pendingOffers } returns offers
        every { ordersRepository.arePendingOffersStale() } returns true
        coEvery { ordersRepository.refreshPendingOffers() } returns ApiResult.Success(emptyList())
    }

    private fun viewModel() = PendingOffersCardViewModel(ordersRepository)

    private fun offer(id: String, respondByUtc: String) = PendingOffer(
        id = id,
        displayOrderNumber = "CL-$id",
        cleaningDateTime = "2026-08-12T09:00:00Z",
        estimatedTime = 120,
        respondByUtc = respondByUtc,
        customerAddressApproximate = "Praha 4 · 14000",
        rooms = 2,
        bathrooms = 1,
        totalPrice = 1200.0,
        currencyCode = "CZK",
    )

    @Test
    fun `no offers means no card`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(PendingOffersCardUiState.Hidden, vm.uiState.value)
    }

    @Test
    fun `offers surface the soonest deadline and how many more there are`() = runTest {
        coEvery { ordersRepository.refreshPendingOffers() } answers {
            offers.value = listOf(
                offer("late", "2026-08-10T20:00:00Z"),
                offer("soon", "2026-08-10T10:00:00Z"),
            )
            ApiResult.Success(offers.value)
        }

        val vm = viewModel()
        advanceUntilIdle()

        val visible = vm.uiState.value as PendingOffersCardUiState.Visible
        assertEquals(2, visible.count)
        assertEquals("2026-08-10T10:00:00Z", visible.soonestRespondByUtc)
    }

    @Test
    fun `a card that cannot answer its own question simply does not appear`() = runTest {
        coEvery { ordersRepository.refreshPendingOffers() } returns
            ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(PendingOffersCardUiState.Hidden, vm.uiState.value)
    }

    @Test
    fun `a warm cache is rendered without a second call`() = runTest {
        every { ordersRepository.arePendingOffersStale() } returns false
        offers.value = listOf(offer("a", "2026-08-10T10:00:00Z"))

        val vm = viewModel()
        advanceUntilIdle()

        coVerify(exactly = 0) { ordersRepository.refreshPendingOffers() }
        assertEquals(1, (vm.uiState.value as PendingOffersCardUiState.Visible).count)
    }
}
