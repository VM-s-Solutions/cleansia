import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class OrdersListViewModelTests: XCTestCase {
    var client: FakePartnerOrderClient!
    var staleness: OrdersStaleness!
    var snackbar: SnackbarController!
    var clock = Date(timeIntervalSince1970: 1000)

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        staleness = OrdersStaleness(window: 30, now: { self.clock })
        snackbar = SnackbarController()
    }

    func makeVM() -> OrdersListViewModel {
        OrdersListViewModel(client: client, staleness: staleness, snackbar: snackbar)
    }

    func testInitialPaneStateIsLoading() {
        let vm = makeVM()
        XCTAssertTrue(vm.currentState.isLoading)
        XCTAssertEqual(vm.tab, .available)
        XCTAssertEqual(vm.refreshPhase, .idle)
    }

    func testOnAppearLoadsAvailableIntoLoaded() async {
        client.pagedResult = .success([.sample(id: "o1")])
        let vm = makeVM()
        await vm.onAppear()
        XCTAssertEqual(vm.currentState.loadedValue?.map(\.id), ["o1"])
        XCTAssertEqual(vm.refreshPhase, .idle)
    }

    func testFailureWithNoCacheTransitionsToErrorAndSnackbars() async {
        client.pagedResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeVM()
        await vm.onAppear()
        guard case .error = vm.currentState else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    // MARK: O3 — "mine" panes send only the own id; Available sends none

    func testAvailablePaneSendsNoEmployeeIdUnassignedTrue() async {
        let vm = makeVM()
        await vm.onAppear()
        let query = try? XCTUnwrap(client.queries.last)
        XCTAssertNil(query?.employeeId)
        XCTAssertEqual(query?.isUnassigned, true)
        XCTAssertEqual(client.employeeIdCallCount, 0) // never resolved for Available
    }

    func testActivePaneSendsOnlyOwnEmployeeId() async {
        client.employeeIdResult = .success("emp-self")
        let vm = makeVM()
        await vm.selectTab(.active)
        let query = try? XCTUnwrap(client.queries.last)
        XCTAssertEqual(query?.employeeId, "emp-self")
        XCTAssertNil(query?.isUnassigned)
    }

    func testHistoryPaneSendsOnlyOwnEmployeeId() async {
        client.employeeIdResult = .success("emp-self")
        let vm = makeVM()
        await vm.selectTab(.history)
        XCTAssertEqual(client.queries.last?.employeeId, "emp-self")
    }

    func testOwnEmployeeIdResolvedOnceAndReused() async {
        client.employeeIdResult = .success("emp-self")
        let vm = makeVM()
        await vm.selectTab(.active)
        staleness.invalidatePanes(for: .startOrder) // force a re-fetch path
        await vm.userRefresh()
        XCTAssertEqual(client.employeeIdCallCount, 1)
    }

    // MARK: PTR — userRefreshing ONLY on user pull, never on background

    func testUserRefreshUsesUserRefreshingPhaseDuringFlight() async {
        client.pagedResult = .success([.sample(id: "o1")])
        let vm = makeVM()
        await vm.onAppear()

        var sawUserRefreshing = false
        let token = vm.$refreshPhase.sink { if $0 == .userRefreshing { sawUserRefreshing = true } }
        defer { token.cancel() }

        await vm.userRefresh()
        XCTAssertTrue(sawUserRefreshing)
        XCTAssertEqual(vm.refreshPhase, .idle)
    }

    func testBackgroundFetchNeverEntersUserRefreshing() async {
        let vm = makeVM()
        var sawUserRefreshing = false
        let token = vm.$refreshPhase.sink { if $0 == .userRefreshing { sawUserRefreshing = true } }
        defer { token.cancel() }

        await vm.onAppear() // background path
        XCTAssertFalse(sawUserRefreshing)
    }

    func testBackgroundFetchKeepsLoadedRowsNoSpinnerFlash() async {
        client.pagedResult = .success([.sample(id: "o1")])
        let vm = makeVM()
        await vm.onAppear()
        // A subsequent stale background fetch must not drop the loaded rows.
        clock = clock.addingTimeInterval(31)
        client.pagedResult = .success([.sample(id: "o2")])
        await vm.onAppear()
        XCTAssertEqual(vm.currentState.loadedValue?.map(\.id), ["o2"])
    }

    // MARK: staleness freshness-skip

    func testWarmCacheSkipsTheNetworkOnReentry() async {
        client.pagedResult = .success([.sample(id: "o1")])
        let vm = makeVM()
        await vm.onAppear()
        XCTAssertEqual(client.getPagedCallCount, 1)
        await vm.onAppear() // still warm → no second fetch
        XCTAssertEqual(client.getPagedCallCount, 1)
    }

    /// THE REGRESSION. The two tests around this one re-enter on the SAME view model, whose `paneState`
    /// still holds the rows — so skipping is right there. In the app the view model does not survive:
    /// `OrdersStaleness` is built once in `PartnerAppContainer` and lives as long as the process, while
    /// `OrdersListView` rebuilds its `@StateObject` and every pane starts at `.loading`. Guarding on the
    /// watermark alone therefore left a fresh view model spinning with nothing on the way, and only a
    /// pull-to-refresh could end it.
    func testAFreshViewModelLoadsEvenWhenTheWatermarkIsStillWarm() async {
        client.pagedResult = .success([.sample(id: "o1")])
        let warm = makeVM()
        await warm.onAppear()
        XCTAssertEqual(client.getPagedCallCount, 1)

        // Same staleness object, brand-new view model — exactly what re-entering the tab does.
        let reentered = makeVM()
        XCTAssertTrue(reentered.currentState.isLoading)
        await reentered.onAppear()

        XCTAssertEqual(client.getPagedCallCount, 2, "the fresh view model never fetched")
        XCTAssertEqual(reentered.currentState.loadedValue?.map(\.id), ["o1"])
    }

    func testStaleCacheRefetchesOnReentry() async {
        client.pagedResult = .success([.sample(id: "o1")])
        let vm = makeVM()
        await vm.onAppear()
        clock = clock.addingTimeInterval(31)
        await vm.onAppear()
        XCTAssertEqual(client.getPagedCallCount, 2)
    }

    // MARK: tab switch + search + sort/period

    func testSelectTabFetchesNewPane() async {
        let vm = makeVM()
        await vm.onAppear()
        await vm.selectTab(.active)
        XCTAssertEqual(vm.tab, .active)
        XCTAssertEqual(client.queries.last?.statuses, [._2, ._3, ._4])
    }

    func testSearchFiltersVisibleOrders() async {
        client.pagedResult = .success([
            .sample(id: "o1", customerName: "Jana"),
            .sample(id: "o2", customerName: "Petr")
        ])
        let vm = makeVM()
        await vm.onAppear()
        vm.setSearchQuery("jana")
        XCTAssertEqual(vm.visibleOrders.map(\.id), ["o1"])
    }

    // MARK: search is scoped to Available — the only pane that renders the field

    // `OrdersListContent.swift` puts `OrdersSearchField` inside `AvailablePane`
    // and nowhere else, so a query left over from Available used to silently
    // filter Active/History with no visible control to clear it — and because
    // `ActivePane`'s empty branch renders `noActiveOrders`, a cleaner could be
    // told they have no jobs on a day they do. These pin the scoping. The
    // Android parity is the same shape: only `AvailablePane` calls
    // `matchesSearch`; `ActivePane`/`HistoryPane` read `uiState.orders` raw.

    func testSearchQueryDoesNotFilterTheActivePane() async {
        client.pagedResult = .success([
            .sample(id: "o1", customerName: "Jana"),
            .sample(id: "o2", customerName: "Petr")
        ])
        let vm = makeVM()
        await vm.onAppear()
        vm.setSearchQuery("jana")
        XCTAssertEqual(vm.visibleOrders.map(\.id), ["o1"], "Available still filters")

        await vm.selectTab(.active)
        XCTAssertEqual(vm.visibleOrders.map(\.id), ["o1", "o2"])
    }

    func testSearchQueryDoesNotFilterTheHistoryPane() async {
        client.pagedResult = .success([
            .sample(id: "o1", customerName: "Jana"),
            .sample(id: "o2", customerName: "Petr")
        ])
        let vm = makeVM()
        await vm.onAppear()
        vm.setSearchQuery("jana")

        await vm.selectTab(.history)
        XCTAssertEqual(vm.visibleOrders.map(\.id), ["o1", "o2"])
    }

    /// Deliberate decision, pinned so a later "tidy-up" can't silently reverse
    /// it: switching tabs does NOT clear the query. Android's `selectTab` only
    /// copies `tab`/`orders` and leaves `searchQuery` alone, and a cleaner who
    /// checks an active job mid-search expects their typing back when they
    /// return. Scoping the *filter* is the fix; clearing the *field* is not.
    func testSearchQuerySurvivesATabRoundTrip() async {
        client.pagedResult = .success([
            .sample(id: "o1", customerName: "Jana"),
            .sample(id: "o2", customerName: "Petr")
        ])
        let vm = makeVM()
        await vm.onAppear()
        vm.setSearchQuery("jana")

        await vm.selectTab(.active)
        await vm.selectTab(.available)

        XCTAssertEqual(vm.searchQuery, "jana")
        XCTAssertEqual(vm.visibleOrders.map(\.id), ["o1"])
    }

    func testSortChangeRefetchesAvailableSilently() async {
        let vm = makeVM()
        await vm.onAppear()
        await vm.setAvailableSort(.priceHighToLow)
        XCTAssertEqual(client.queries.last?.sortField, "totalPrice")
    }

    func testPeriodChangeRefetchesHistorySilently() async {
        client.employeeIdResult = .success("emp-self")
        let vm = makeVM()
        await vm.selectTab(.history)
        let before = client.getPagedCallCount
        await vm.setCompletedPeriod(.lastMonth)
        XCTAssertEqual(client.getPagedCallCount, before + 1)
        XCTAssertEqual(vm.completedPeriod, .lastMonth)
    }

    func testInProgressOrderDrivesBanner() async {
        client.pagedResult = .success([
            .sample(id: "o1", status: ._2),
            .sample(id: "o2", status: ._4)
        ])
        let vm = makeVM()
        await vm.selectTab(.active)
        XCTAssertEqual(vm.inProgressOrder?.id, "o2")
    }

    // MARK: navigation effect (VM emits, never navigates)

    func testOpenDetailEmitsNavigateEffect() {
        let vm = makeVM()
        var captured: String?
        let token = vm.navigateToDetail.sink { captured = $0 }
        defer { token.cancel() }
        vm.openDetail("o1")
        XCTAssertEqual(captured, "o1")
    }

    // MARK: TC-IOS-ORDERS-OWNERSHIP (O1 / O2)

    func testInlineActionActsOnlyOnRowIdNoEmployeeId() async {
        client.pagedResult = .success([.sample(id: "row-id", status: ._3)])
        let vm = makeVM()
        await vm.selectTab(.active)

        await vm.runInlineAction(.start, on: .sample(id: "row-id", status: ._3))

        // O1: the command surface carries only orderId. O2: the carried id is
        // the row's own id from the list response.
        XCTAssertEqual(client.commands.first?.orderId, "row-id")
    }

    func testTheContractSheetIsOpenedOnTheRowsOwnIdAlone() async {
        client.pagedResult = .success([.sample(id: "row-id", status: ._2)])
        let vm = makeVM()
        await vm.onAppear()

        await vm.runInlineAction(.take, on: .sample(id: "row-id", status: ._2))

        XCTAssertEqual(vm.contractRequest, .take(orderId: "row-id"))
    }

    // MARK: - Location (distance on the Available rows)

    final class FakeLocationProvider: LocationProvider {
        var status: LocationAuthorizationStatus = .notDetermined
        var statusAfterRequest: LocationAuthorizationStatus = .denied
        var fix: Coordinate?
        private(set) var requestCount = 0
        private(set) var currentLocationCount = 0

        var authorizationStatus: LocationAuthorizationStatus {
            status
        }

        func requestWhenInUseAuthorization() async -> LocationAuthorizationStatus {
            requestCount += 1
            status = statusAfterRequest
            return status
        }

        func currentLocation() async -> Coordinate? {
            currentLocationCount += 1
            return fix
        }
    }

    static let pragueFix = Coordinate(latitude: 50.08, longitude: 14.44)

    /// The guard for the whole design decision: entering the Available tab must
    /// never raise the system dialog by itself. iOS grants exactly one prompt per
    /// install, so an auto-prompt seconds after login would burn it cold.
    func testRefreshDoesNotPromptWhenNotDetermined() async {
        let provider = FakeLocationProvider()
        provider.status = .notDetermined
        provider.fix = Self.pragueFix
        let vm = makeVM()

        await vm.refreshLocationIfAuthorized(provider)

        XCTAssertEqual(provider.requestCount, 0)
        XCTAssertEqual(provider.currentLocationCount, 0)
        XCTAssertNil(vm.currentLocation)
        XCTAssertTrue(vm.showsLocationPrompt)
    }

    func testRefreshAssignsTheFixWhenAuthorized() async {
        let provider = FakeLocationProvider()
        provider.status = .authorized
        provider.fix = Self.pragueFix
        let vm = makeVM()

        await vm.refreshLocationIfAuthorized(provider)

        XCTAssertEqual(vm.currentLocation, Self.pragueFix)
        XCTAssertEqual(vm.locationStatus, .authorized)
        XCTAssertEqual(provider.requestCount, 0)
        XCTAssertFalse(vm.showsLocationPrompt)
    }

    func testRefreshIsSilentWhenDenied() async {
        let provider = FakeLocationProvider()
        provider.status = .denied
        provider.fix = Self.pragueFix
        let vm = makeVM()

        await vm.refreshLocationIfAuthorized(provider)

        XCTAssertEqual(provider.requestCount, 0)
        XCTAssertEqual(provider.currentLocationCount, 0)
        XCTAssertNil(vm.currentLocation)
        XCTAssertFalse(vm.showsLocationPrompt)
    }

    func testExplicitRequestPromptsOnceAndAssignsTheFix() async {
        let provider = FakeLocationProvider()
        provider.status = .notDetermined
        provider.statusAfterRequest = .authorized
        provider.fix = Self.pragueFix
        let vm = makeVM()

        await vm.requestLocationPermission(provider)

        XCTAssertEqual(provider.requestCount, 1)
        XCTAssertEqual(vm.locationStatus, .authorized)
        XCTAssertEqual(vm.currentLocation, Self.pragueFix)
    }

    /// A refused prompt must retire the row rather than leave it lingering —
    /// iOS will not show the dialog again, so the button would be inert.
    func testExplicitRequestDeniedLeavesDistanceHidden() async {
        let provider = FakeLocationProvider()
        provider.status = .notDetermined
        provider.statusAfterRequest = .denied
        provider.fix = Self.pragueFix
        let vm = makeVM()

        await vm.requestLocationPermission(provider)

        XCTAssertEqual(provider.requestCount, 1)
        XCTAssertEqual(provider.currentLocationCount, 0)
        XCTAssertNil(vm.currentLocation)
        XCTAssertFalse(vm.showsLocationPrompt)
    }

    func testPromptOnlyShowsOnTheAvailableTab() async {
        let vm = makeVM()
        XCTAssertTrue(vm.showsLocationPrompt)

        await vm.selectTab(.active)

        XCTAssertFalse(vm.showsLocationPrompt)
    }
}
