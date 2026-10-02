package cz.cleansia.customer.features.home

import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.market.MarketListItem
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.MembershipPlanDto
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.notifications.NotificationFeedRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
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
    private lateinit var marketRepository: MarketRepository
    private lateinit var snackbar: SnackbarController

    private lateinit var loyaltyStaleness: Staleness
    private lateinit var orderStaleness: Staleness
    private lateinit var membershipStaleness: Staleness
    private lateinit var marketStaleness: Staleness

    @Before
    fun setUp() {
        addressRepository = mockk(relaxed = true)
        orderRepository = mockk(relaxed = true)
        loyaltyRepository = mockk(relaxed = true)
        membershipRepository = mockk(relaxed = true)
        catalogRepository = mockk(relaxed = true)
        recurringBookingRepository = mockk(relaxed = true)
        notificationFeedRepository = mockk(relaxed = true)
        marketRepository = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)

        loyaltyStaleness = Staleness()
        orderStaleness = Staleness()
        membershipStaleness = Staleness()
        marketStaleness = Staleness()
        every { loyaltyRepository.staleness } returns loyaltyStaleness
        every { orderRepository.staleness } returns orderStaleness
        every { membershipRepository.staleness } returns membershipStaleness
        every { marketRepository.staleness } returns marketStaleness
        coEvery { marketRepository.ensureLoaded() } returns MarketState.Unavailable
        coEvery { catalogRepository.refresh(any()) } returns ApiResult.Success(Unit)
        every { membershipRepository.current } returns membership
        coEvery { membershipRepository.getPlans(any(), any()) } returns ApiResult.Success(emptyList())
    }

    private val membership = MutableStateFlow<GetMyMembershipResponse?>(null)

    private fun plan(billingInterval: Int, trialDays: Int) = MembershipPlanDto(
        code = "plus_$billingInterval",
        name = "Cleansia Plus",
        price = 199.0,
        monthlyEquivalentPrice = 199.0,
        billingInterval = billingInterval,
        discountPercentage = 5.0,
        freeCancellationWindowHours = 4,
        allowsExpressUpgrade = true,
        trialPeriodDays = trialDays,
        savingsPercentVsMonthly = 0.0,
        currencyCode = "EUR",
    )

    private fun market(iso: String) = MarketListItem(
        countryId = "$iso-id",
        isoCode = iso,
        isoAlpha2 = iso.take(2),
        name = iso,
        currencyId = "cur",
        currencyCode = "EUR",
        currencySymbol = "€",
        isDefault = false,
    )

    private fun newViewModel() = HomeTabViewModel(
        addressRepository = addressRepository,
        orderRepository = orderRepository,
        loyaltyRepository = loyaltyRepository,
        membershipRepository = membershipRepository,
        catalogRepository = catalogRepository,
        recurringBookingRepository = recurringBookingRepository,
        notificationFeedRepository = notificationFeedRepository,
        marketRepository = marketRepository,
        snackbar = snackbar,
        referralRepository = io.mockk.mockk(relaxed = true),
        appSettings = io.mockk.mockk(relaxed = true),
    )

    // ADR-0058 D5: the home catalogue is read for the chosen market, and the directory is read first
    // so the first catalogue request already carries it rather than being paid twice.

    @Test
    fun refreshCatalog_readsTheDirectoryFirstAndSendsTheChosenMarket() = runTest {
        val svk = market("SVK")
        coEvery { marketRepository.ensureLoaded() } returns MarketState.Resolved(listOf(svk), svk)

        newViewModel().refreshCatalog()
        advanceUntilIdle()

        coVerify(exactly = 1) { catalogRepository.refresh("SVK-id") }
        coVerify(exactly = 0) { catalogRepository.refresh(null) }
    }

    @Test
    fun refreshCatalog_withTheDirectoryUnavailable_sendsNoCountry() = runTest {
        newViewModel().refreshCatalog()
        advanceUntilIdle()

        coVerify(exactly = 1) { catalogRepository.refresh(null) }
    }

    // "Try Plus free" is offered only while this customer can still get a trial, for the days the
    // chosen market's plan carries.

    @Test
    fun plusTrialDays_offersTheChosenMarketsMonthlyTrialToACustomerWhoNeverHadOne() = runTest {
        val svk = market("SVK")
        coEvery { marketRepository.ensureLoaded() } returns MarketState.Resolved(listOf(svk), svk)
        coEvery { membershipRepository.getPlans("SVK-id", any()) } returns ApiResult.Success(
            listOf(plan(billingInterval = 2, trialDays = 30), plan(billingInterval = 1, trialDays = 14)),
        )
        membership.value = GetMyMembershipResponse(hasMembership = false, trialEligible = true)

        val vm = newViewModel()
        vm.refreshPlusPlans()
        advanceUntilIdle()

        assertEquals(14, vm.plusTrialDays.value)
    }

    @Test
    fun plusTrialDays_offersNoneToACustomerWhoHasHadTheirTrial() = runTest {
        coEvery { membershipRepository.getPlans(null, any()) } returns
            ApiResult.Success(listOf(plan(billingInterval = 1, trialDays = 14)))
        membership.value = GetMyMembershipResponse(hasMembership = false, trialEligible = false)

        val vm = newViewModel()
        vm.refreshPlusPlans()
        advanceUntilIdle()

        assertEquals(0, vm.plusTrialDays.value)
    }

    @Test
    fun plusTrialDays_offersNoneWhenThePlansCannotBeRead() = runTest {
        coEvery { membershipRepository.getPlans(any(), any()) } returns ApiResult.Error(ApiError.Network("offline"))
        membership.value = GetMyMembershipResponse(hasMembership = false, trialEligible = true)

        val vm = newViewModel()
        vm.refreshPlusPlans()
        advanceUntilIdle()

        assertEquals(0, vm.plusTrialDays.value)
    }

    @Test
    fun onResume_refreshesTheDirectoryOnlyWhileStale() = runTest {
        val vm = newViewModel()
        vm.onResume()
        advanceUntilIdle()
        coVerify(exactly = 1) { marketRepository.refresh() }

        marketStaleness.markFresh()
        vm.onResume()
        advanceUntilIdle()
        coVerify(exactly = 1) { marketRepository.refresh() }
    }

    @Test
    fun pullToRefresh_reReadsTheDirectoryUngated() = runTest {
        marketStaleness.markFresh()

        newViewModel().pullToRefresh()
        advanceUntilIdle()

        coVerify(exactly = 1) { marketRepository.refresh() }
    }

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
