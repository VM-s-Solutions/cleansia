import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// Pins the freshness gate on `HomeTabViewModel.refreshStaleSources()`.
///
/// The hook runs from Home's `.task` — which SwiftUI restarts every time the tab
/// is re-selected — and from the `scenePhase` foreground transition. These tests
/// are what stop it from becoming three network calls per tab tap.
///
/// Real `Staleness` instances on a stopped clock, so the gate under test is the
/// shipped one.
@MainActor
final class HomeTabViewModelTests: XCTestCase {
    private var clock = Date(timeIntervalSince1970: 1_000_000)
    private var loyaltyClient: FakeLoyaltyClient!
    private var orderClient: FakeOrderClient!
    private var membershipClient: FakeMembershipManagementClient!
    private var loyaltyRepository: LoyaltyRepository!
    private var orderRepository: OrderRepository!
    private var membershipRepository: MembershipRepository!

    override func setUp() {
        super.setUp()
        clock = Date(timeIntervalSince1970: 1_000_000)
        loyaltyClient = FakeLoyaltyClient()
        orderClient = FakeOrderClient()
        membershipClient = FakeMembershipManagementClient()
        loyaltyRepository = LoyaltyRepository(client: loyaltyClient, staleness: makeStaleness())
        orderRepository = OrderRepository(client: orderClient, staleness: makeStaleness())
        membershipRepository = MembershipRepository(client: membershipClient, staleness: makeStaleness())
    }

    override func tearDown() {
        loyaltyClient = nil
        orderClient = nil
        membershipClient = nil
        loyaltyRepository = nil
        orderRepository = nil
        membershipRepository = nil
        super.tearDown()
    }

    private func makeStaleness() -> Staleness {
        Staleness(window: 30, now: { self.clock })
    }

    private func makeViewModel(
        marketStore: MarketStore? = nil,
        catalog: FakeCatalogClient = FakeCatalogClient()
    ) -> HomeTabViewModel {
        let marketStore = marketStore ?? MarketStore(
            client: FakeMarketClient(),
            preference: FakeMarketPreferenceStore()
        )
        return HomeTabViewModel(
            orderRepository: orderRepository,
            recurringRepository: RecurringBookingRepository(client: FakeRecurringBookingClient()),
            loyaltyRepository: loyaltyRepository,
            membershipRepository: membershipRepository,
            savedAddressRepository: SavedAddressRepository(client: FakeSavedAddressClient()),
            marketStore: marketStore,
            catalogSource: BookingViewModel(catalogClient: catalog, market: marketStore.statePublisher),
            snackbar: SnackbarController()
        )
    }

    // MARK: The Plus slide's trial — the chosen market's monthly plan, once per account

    func testThePlusSlideOffersTheMonthlyTrialToACustomerWhoNeverHadOne() async {
        membershipClient.mineResults = [.success(nonMember(trialEligible: true))]
        membershipClient.plansResult = .success(plans(monthlyTrialDays: 14, yearlyTrialDays: 30))
        let vm = makeViewModel()
        await membershipRepository.refresh()

        await vm.refreshPlusPlans()

        XCTAssertEqual(vm.plusTrialDays, 14)
    }

    func testThePlusSlideOffersNoTrialToACustomerWhoHasHadTheirs() async {
        membershipClient.mineResults = [.success(nonMember(trialEligible: false))]
        membershipClient.plansResult = .success(plans(monthlyTrialDays: 14, yearlyTrialDays: 14))
        let vm = makeViewModel()
        await membershipRepository.refresh()

        await vm.refreshPlusPlans()

        XCTAssertEqual(vm.plusTrialDays, 0)
    }

    func testThePlusSlideOffersNoTrialWhenThePlansCannotBeRead() async {
        membershipClient.mineResults = [.success(nonMember(trialEligible: true))]
        membershipClient.plansResult = .failure(ApiError(httpStatus: 500))
        let vm = makeViewModel()
        await membershipRepository.refresh()

        await vm.refreshPlusPlans()

        XCTAssertEqual(vm.plusTrialDays, 0)
    }

    func testAMemberIsNeverSentForThePlans() async {
        membershipClient.mineResults = [.success(MembershipFixtures.active)]
        let vm = makeViewModel()
        await membershipRepository.refresh()

        await vm.refreshPlusPlans()

        XCTAssertEqual(membershipClient.plansCallCount, 0)
    }

    private func nonMember(trialEligible: Bool) -> MyMembership {
        MyMembership(
            hasMembership: false,
            planCode: nil,
            planName: nil,
            discountPercentage: nil,
            freeCancellationWindowHours: nil,
            allowsExpressUpgrade: nil,
            currentPeriodEnd: nil,
            cancelRequested: false,
            billingInterval: nil,
            trialEligible: trialEligible
        )
    }

    /// The yearly plan listed first, so the monthly one is found rather than taken by position.
    private func plans(monthlyTrialDays: Int, yearlyTrialDays: Int) -> [MembershipPlan] {
        MembershipFixtures.plans.reversed().map { plan in
            MembershipPlan(
                code: plan.code,
                name: plan.name,
                price: plan.price,
                monthlyEquivalentPrice: plan.monthlyEquivalentPrice,
                billingInterval: plan.billingInterval,
                discountPercentage: plan.discountPercentage,
                freeCancellationWindowHours: plan.freeCancellationWindowHours,
                allowsExpressUpgrade: plan.allowsExpressUpgrade,
                expressUpgradesPerMonth: plan.expressUpgradesPerMonth,
                trialPeriodDays: plan.isAnnual ? yearlyTrialDays : monthlyTrialDays,
                savingsPercentVsMonthly: plan.savingsPercentVsMonthly,
                currencyCode: plan.currencyCode
            )
        }
    }

    // MARK: The market chip

    func testTheChipShowsTheChosenMarketOnlyWhenThereIsAChoice() async {
        let two = await makeViewModel(marketStore: MarketFixtures.resolved(selected: MarketFixtures.slovakia))
        XCTAssertEqual(two.marketChip?.chipLabel, "SK · EUR")

        let one = await makeViewModel(marketStore: MarketFixtures.resolved(MarketFixtures.one))
        XCTAssertNil(one.marketChip)

        let none = await makeViewModel(marketStore: MarketFixtures.unavailable())
        XCTAssertNil(none.marketChip)
    }

    func testTheChipFollowsASwitchWithoutARestart() async {
        let store = await MarketFixtures.resolved()
        let vm = makeViewModel(marketStore: store)
        XCTAssertEqual(vm.marketChip?.chipLabel, "CZ · CZK")

        store.select(isoCode: "SVK")

        XCTAssertEqual(vm.marketChip?.chipLabel, "SK · EUR")
    }

    /// The catalogue is priced for the chosen market, so Home reads the directory first and the
    /// catalogue once, for that market — never once for the default and again for the market.
    func testTheCatalogueIsReadOnceForTheChosenMarket() async {
        let (store, _, _) = MarketFixtures.store(stored: "SVK")
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.slovak))
        let vm = makeViewModel(marketStore: store, catalog: catalog)

        await vm.refreshCatalogIfNeeded()

        XCTAssertEqual(catalog.requestedCountryIds, ["svk"])
    }

    func testWithoutADirectoryTheCatalogueIsReadForTheDefaultAndNothingIsPersisted() async {
        let (store, _, preference) = MarketFixtures.store(.failure(ApiError(httpStatus: 500)))
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let vm = makeViewModel(marketStore: store, catalog: catalog)

        await vm.refreshCatalogIfNeeded()

        XCTAssertEqual(catalog.requestedCountryIds, [nil])
        XCTAssertTrue(preference.writes.isEmpty)
        XCTAssertNil(vm.marketChip)
    }

    func testHomeEntryRetriesAFailedDirectoryRead() async {
        let (store, client, _) = MarketFixtures.store(.failure(ApiError(httpStatus: 500)))
        await store.refresh()
        client.result = .success(MarketFixtures.two)
        let vm = makeViewModel(marketStore: store)

        await vm.refreshMarket()

        XCTAssertEqual(client.callCount, 2)
        XCTAssertEqual(vm.marketChip?.chipLabel, "CZ · CZK")
    }

    private var loyaltyCalls: Int {
        loyaltyClient.accountCallCount
    }

    private var orderCalls: Int {
        orderClient.pageRequests.count
    }

    private var membershipCalls: Int {
        membershipClient.mineCallCount
    }

    func testColdEntryFetchesEverySource() async {
        await makeViewModel().refreshStaleSources()

        XCTAssertEqual(loyaltyCalls, 1)
        XCTAssertEqual(orderCalls, 1)
        XCTAssertEqual(membershipCalls, 1)
    }

    func testFreshCachesCostNoNetwork() async {
        loyaltyRepository.staleness.markFresh()
        orderRepository.staleness.markFresh()
        membershipRepository.staleness.markFresh()

        await makeViewModel().refreshStaleSources()

        XCTAssertEqual(loyaltyCalls, 0)
        XCTAssertEqual(orderCalls, 0)
        XCTAssertEqual(membershipCalls, 0)
    }

    func testRepeatedTabReturnsInsideTheWindowCostOneFetchEach() async {
        // The failure this guards: an ungated hook billing three network calls
        // every time the user taps back onto Home.
        let vm = makeViewModel()

        for _ in 0 ..< 6 {
            await vm.refreshStaleSources()
        }

        XCTAssertEqual(loyaltyCalls, 1)
        XCTAssertEqual(orderCalls, 1)
        XCTAssertEqual(membershipCalls, 1)
    }

    func testReturningAfterTheWindowRefetches() async {
        // The bug itself: points earned mid-session have to reach the milestone
        // card without a cold start.
        let vm = makeViewModel()
        await vm.refreshStaleSources()

        clock = clock.addingTimeInterval(31)
        await vm.refreshStaleSources()

        XCTAssertEqual(loyaltyCalls, 2)
        XCTAssertEqual(orderCalls, 2)
        XCTAssertEqual(membershipCalls, 2)
    }

    func testGatesEachSourceIndependently() async {
        loyaltyRepository.staleness.markFresh()

        await makeViewModel().refreshStaleSources()

        XCTAssertEqual(loyaltyCalls, 0)
        XCTAssertEqual(orderCalls, 1)
        XCTAssertEqual(membershipCalls, 1)
    }

    func testSignOutClearMakesTheNextEntryRefetch() async {
        // `clear()` is what the SessionScopedCacheRegistry calls on sign-out /
        // forced 401. Without the watermark reset the next account would read
        // the previous account's loyalty as fresh.
        let vm = makeViewModel()
        await vm.refreshStaleSources()

        await loyaltyRepository.clear()
        await orderRepository.clear()
        await membershipRepository.clear()
        await vm.refreshStaleSources()

        XCTAssertEqual(loyaltyCalls, 2)
        XCTAssertEqual(orderCalls, 2)
        XCTAssertEqual(membershipCalls, 2)
    }
}
