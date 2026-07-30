package cz.cleansia.customer.features.home

import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.notifications.NotificationFeedRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * Pins the freshness gate on [HomeTabViewModel.onResume].
 *
 * The hook is driven by a `LifecycleEventEffect(ON_START)`, which re-dispatches
 * to an already-STARTED lifecycle every time HomeTab recomposes — so it fires on
 * every tab switch back to Home, not once per foreground. These tests are what
 * stop it from turning into three network calls per tab tap.
 *
 * Real [Staleness] instances (not mocks) so the gate under test is the shipped one.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class HomeTabViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var addressRepository: AddressRepository
    private lateinit var orderRepository: OrderRepository
    private lateinit var loyaltyRepository: LoyaltyRepository
    private lateinit var membershipRepository: MembershipRepository
    private lateinit var catalogRepository: CatalogRepository
    private lateinit var recurringBookingRepository: RecurringBookingRepository
    private lateinit var notificationFeedRepository: NotificationFeedRepository
    private lateinit var snackbar: SnackbarController

    private lateinit var loyaltyStaleness: Staleness
    private lateinit var orderStaleness: Staleness
    private lateinit var membershipStaleness: Staleness

    @Before
    fun setUp() {
        addressRepository = mockk(relaxed = true)
        orderRepository = mockk(relaxed = true)
        loyaltyRepository = mockk(relaxed = true)
        membershipRepository = mockk(relaxed = true)
        catalogRepository = mockk(relaxed = true)
        recurringBookingRepository = mockk(relaxed = true)
        notificationFeedRepository = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)

        loyaltyStaleness = Staleness()
        orderStaleness = Staleness()
        membershipStaleness = Staleness()
        every { loyaltyRepository.staleness } returns loyaltyStaleness
        every { orderRepository.staleness } returns orderStaleness
        every { membershipRepository.staleness } returns membershipStaleness
    }

    private fun newViewModel() = HomeTabViewModel(
        addressRepository = addressRepository,
        orderRepository = orderRepository,
        loyaltyRepository = loyaltyRepository,
        membershipRepository = membershipRepository,
        catalogRepository = catalogRepository,
        recurringBookingRepository = recurringBookingRepository,
        notificationFeedRepository = notificationFeedRepository,
        snackbar = snackbar,
    )

    @Test
    fun onResume_givenNeverFetchedCaches_refreshesAllThree() = runTest {
        newViewModel().onResume()
        advanceUntilIdle()

        coVerify(exactly = 1) { loyaltyRepository.refresh() }
        coVerify(exactly = 1) { orderRepository.refresh() }
        coVerify(exactly = 1) { membershipRepository.refresh() }
    }

    @Test
    fun onResume_givenFreshCaches_doesNotHitTheNetwork() = runTest {
        loyaltyStaleness.markFresh()
        orderStaleness.markFresh()
        membershipStaleness.markFresh()

        newViewModel().onResume()
        advanceUntilIdle()

        coVerify(exactly = 0) { loyaltyRepository.refresh() }
        coVerify(exactly = 0) { orderRepository.refresh() }
        coVerify(exactly = 0) { membershipRepository.refresh() }
    }

    @Test
    fun onResume_repeatedTabReturnsCostAtMostOneFetchPerWindow() = runTest {
        // The failure this guards: an ungated hook billing a network call every
        // time the user taps back onto Home. The first pass is unstubbed-stale
        // and fetches, so mark fresh the way a successful refresh would.
        val vm = newViewModel()
        vm.onResume()
        advanceUntilIdle()
        loyaltyStaleness.markFresh()
        orderStaleness.markFresh()
        membershipStaleness.markFresh()

        repeat(5) {
            vm.onResume()
            advanceUntilIdle()
        }

        coVerify(exactly = 1) { loyaltyRepository.refresh() }
        coVerify(exactly = 1) { orderRepository.refresh() }
        coVerify(exactly = 1) { membershipRepository.refresh() }
    }

    @Test
    fun onResume_gatesEachSourceIndependently() = runTest {
        loyaltyStaleness.markFresh()

        newViewModel().onResume()
        advanceUntilIdle()

        coVerify(exactly = 0) { loyaltyRepository.refresh() }
        coVerify(exactly = 1) { orderRepository.refresh() }
        coVerify(exactly = 1) { membershipRepository.refresh() }
    }

    @Test
    fun onResume_afterSignOutResetsWatermark_refetches() = runTest {
        // clear() resets the watermark; the next Home entry must refetch rather
        // than serve the previous session's snapshot as fresh.
        loyaltyStaleness.markFresh()
        loyaltyStaleness.reset()

        newViewModel().onResume()
        advanceUntilIdle()

        coVerify(exactly = 1) { loyaltyRepository.refresh() }
    }
}
