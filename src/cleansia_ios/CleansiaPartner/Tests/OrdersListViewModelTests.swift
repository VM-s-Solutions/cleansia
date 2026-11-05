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

    func makeVM(
        payroll: PartnerPayrollClient? = nil,
        offers: PendingOffersStore? = nil,
        boardStore: OrdersBoardStore? = nil,
        now: Date? = nil
    ) -> OrdersListViewModel {
        OrdersListViewModel(
            client: client,
            staleness: staleness,
            snackbar: snackbar,
            payroll: payroll,
            pendingOffers: offers,
            boardStore: boardStore,
            now: { now ?? Date() }
        )
    }

    /// Only the page requests of one pane: Available asks for free seats, Active for statuses 2-4, History for 5.
    func queries(_ pane: OrdersPane) -> [OrderPageQuery] {
        client.queries.filter { query in
            switch pane {
            case .available: query.hasAvailableSpots == true
            case .active: query.statuses == [._2, ._3, ._4]
            case .history: query.statuses == [._5]
            }
        }
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

    /// A list read that fails says so in the pane (and to VoiceOver), never in a snackbar (D17).
    func testAFailedLoadWithNoRowsIsTheErrorStateWithoutASnackbar() async {
        client.pagedResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeVM()
        await vm.onAppear()
        guard case .error = vm.currentState else { return XCTFail("expected error") }
        XCTAssertNil(snackbar.current)
    }

    // MARK: current cleaner scope

    func testAvailablePaneExcludesOnlyTheResolvedCurrentEmployee() async throws {
        let vm = makeVM()
        await vm.onAppear()
        let query = try XCTUnwrap(queries(.available).last)
        XCTAssertNil(query.employeeId)
        XCTAssertNil(query.isUnassigned)
        XCTAssertEqual(query.excludeEmployeeId, "emp-self")
        XCTAssertEqual(query.limit, 100)
        XCTAssertEqual(client.employeeIdCallCount, 1, "Active reuses the id Available just read")
    }

    func testActivePaneSendsOnlyOwnEmployeeId() async {
        let vm = makeVM()
        await vm.selectTab(.active)
        XCTAssertEqual(queries(.active).last?.employeeId, "emp-self")
        XCTAssertNil(queries(.active).last?.isUnassigned)
    }

    func testHistoryPaneSendsOnlyOwnEmployeeId() async {
        let vm = makeVM()
        await vm.selectTab(.history)
        XCTAssertEqual(queries(.history).last?.employeeId, "emp-self")
    }

    func testOwnEmployeeIdResolvedOnceAndReused() async {
        let vm = makeVM()
        await vm.selectTab(.active)
        await vm.userRefresh()
        XCTAssertEqual(client.employeeIdCallCount, 1)
    }

    /// banner.md §3a test 2: Active and History are "mine" only when the request names the cleaner. An id that
    /// cannot be read must end in the error state, never in an unscoped request the server answers with open
    /// jobs that are not theirs.
    func testActiveAndHistoryIdentityFailureNeverSendsAnUnscopedRequest() async {
        for tab in [OrdersTab.active, .history] {
            client = FakePartnerOrderClient()
            client.employeeIdResult = .failure(ApiError(code: "network.unreachable"))
            let vm = makeVM()
            await vm.selectTab(tab)
            XCTAssertFalse(
                client.queries.contains { $0.employeeId == nil && $0.excludeEmployeeId == nil },
                "\(tab) sent an unscoped page request"
            )
            guard case .error = vm.currentState else { return XCTFail("\(tab): expected the error state") }
        }
    }

    func testAvailableIdentityFailureNeverSendsAnUnscopedRequest() async {
        client.employeeIdResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeVM()
        await vm.onAppear()
        XCTAssertEqual(client.getPagedCallCount, 0)
        guard case .error = vm.currentState else { return XCTFail("expected unresolved identity error") }
        XCTAssertTrue(staleness.isPaneStale(.available))
    }

    func testAvailableBlankIdentityNeverSendsAnUnscopedRequest() async {
        for id in ["", " ", "\n\t"] {
            client = FakePartnerOrderClient()
            client.employeeIdResult = .success(id)
            let vm = makeVM()
            await vm.onAppear()
            XCTAssertEqual(client.getPagedCallCount, 0, "blank id: \(id.debugDescription)")
            guard case .error = vm.currentState else { return XCTFail("expected blank identity error") }
            XCTAssertTrue(staleness.isPaneStale(.available))
        }
    }

    /// Rows held and the refresh failed: the rows stay, dated by the stale strip; the next success clears it.
    func testLoadedAvailableIdentityFailureRetainsRowsAndRetries() async {
        client.pagedResult = .success([.sample(id: "last-good")])
        let vm = makeVM()
        await vm.onAppear()
        client.employeeIdResult = .failure(ApiError(code: "network.unreachable"))
        await vm.userRefresh()
        XCTAssertEqual(vm.currentState.loadedValue?.map(\.id), ["last-good"])
        XCTAssertNotNil(vm.pane(.available).error, "the stale strip shows")
        XCTAssertNotNil(vm.pane(.available).loadedAt)
        client.employeeIdResult = .success("emp-self")
        await vm.userRefresh()
        XCTAssertNil(vm.pane(.available).error)
    }

    // MARK: pulls and freshness

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
        await vm.onAppear()
        XCTAssertFalse(sawUserRefreshing)
    }

    func testBackgroundFetchKeepsLoadedRowsNoSpinnerFlash() async {
        client.pagedResult = .success([.sample(id: "o1")])
        let vm = makeVM()
        await vm.onAppear()
        clock = clock.addingTimeInterval(31)
        client.pagedResult = .success([.sample(id: "o2")])
        await vm.onAppear()
        XCTAssertEqual(vm.currentState.loadedValue?.map(\.id), ["o2"])
    }

    func testWarmCacheSkipsTheNetworkOnReentry() async {
        let vm = makeVM()
        await vm.onAppear()
        XCTAssertEqual(queries(.available).count, 1)
        await vm.onAppear()
        XCTAssertEqual(queries(.available).count, 1)
    }

    /// `OrdersStaleness` outlives the view model, whose panes start empty: a fresh view model must load even
    /// when the watermark is still warm, or it spins with nothing on the way.
    func testAFreshViewModelLoadsEvenWhenTheWatermarkIsStillWarm() async {
        client.pagedResult = .success([.sample(id: "o1")])
        await makeVM().onAppear()
        let reentered = makeVM()
        XCTAssertTrue(reentered.currentState.isLoading)
        await reentered.onAppear()
        XCTAssertEqual(queries(.available).count, 2, "the fresh view model never fetched")
        XCTAssertEqual(reentered.currentState.loadedValue?.map(\.id), ["o1"])
    }

    func testStaleCacheRefetchesOnReentry() async {
        let vm = makeVM()
        await vm.onAppear()
        clock = clock.addingTimeInterval(31)
        await vm.onAppear()
        XCTAssertEqual(queries(.available).count, 2)
    }

    /// A pull while the same pane is already loading waits for that load instead of returning at once, so the
    /// indicator never ends before the rows land.
    func testAPullWhileThePaneLoadsAwaitsTheRunningFetch() async {
        client.pagedResult = .success([.sample(id: "o1")])
        client.suspendGetPaged = true
        let vm = makeVM()
        let appear = Task { await vm.onAppear() }
        while client.getPagedCallCount == 0 {
            await Task.yield()
        }
        var pullDone = false
        let pull = Task {
            await vm.userRefresh()
            pullDone = true
        }
        for _ in 0 ..< 20 {
            await Task.yield()
        }
        XCTAssertFalse(pullDone, "the pull returned while the page was still loading")
        client.resumeGetPaged()
        await pull.value
        await appear.value
        XCTAssertTrue(pullDone)
        XCTAssertEqual(vm.currentState.loadedValue?.map(\.id), ["o1"])
    }

    /// A period with jobs reads only itself; an empty one also reads last month, once, for its reference block.
    func testSelectTabFetchesNewPane() async {
        client.pagedResult = .success([.sample(id: "done", status: ._5)])
        let vm = makeVM()
        await vm.selectTab(.history)
        XCTAssertEqual(vm.tab, .history)
        XCTAssertEqual(queries(.history).count, 1)
    }

    func testPeriodChangeRefetchesHistoryToCompletion() async {
        let vm = makeVM()
        await vm.selectTab(.history)
        let before = queries(.history).count
        await vm.setCompletedPeriod(.lastMonth)
        XCTAssertEqual(queries(.history).count, before + 1)
        XCTAssertEqual(vm.completedPeriod, .lastMonth)
    }

    /// A period chosen while the previous one is still loading wins: the old period's page never lands under the
    /// new name, and the new period is the one fetched.
    func testAPeriodChosenWhileHistoryLoadsIsFetchedAndOnlyItsRowsShow() async {
        let lastMonth = OrdersQueryBuilder.periodToDateRange(.lastMonth).from
        client.pageResponder = { query in
            guard query.statuses == [._5] else { return .success(OrderPage(rows: [], total: 0)) }
            let id = query.cleaningDateFrom == lastMonth ? "last-month" : "this-month"
            return .success(OrderPage(rows: [.sample(id: id, status: ._5)], total: 1))
        }
        let vm = makeVM()
        client.suspendGetPaged = true
        let open = Task { await vm.selectTab(.history) }
        while queries(.history).isEmpty {
            await Task.yield()
        }
        let pick = Task { await vm.setCompletedPeriod(.lastMonth) }
        for _ in 0 ..< 20 {
            await Task.yield()
        }
        client.resumeGetPaged()
        await open.value
        await pick.value
        XCTAssertEqual(vm.completedPeriod, .lastMonth)
        XCTAssertEqual(vm.pane(.history).rows.map(\.id), ["last-month"], "this month's page landed under last month")
        XCTAssertEqual(queries(.history).last?.cleaningDateFrom, lastMonth)
    }

    // MARK: the in-progress banner is gone; the hero reads Active only

    /// banner.md §1: an Available row in progress is someone else's started job with a seat open. It must
    /// never be presented as the cleaner's own job in progress.
    func testAStartedJobOnTheBoardIsNeverTheCleanersOwnJobInProgress() async {
        client.pageResponder = { query in
            let rows: [OrderListItem] = query.hasAvailableSpots == true ? [.sample(id: "theirs", status: ._4)] : []
            return .success(OrderPage(rows: rows, total: rows.count))
        }
        let vm = makeVM()
        await vm.onAppear()
        XCTAssertNil(vm.hero, "another cleaner's started job became the cleaner's own")
        XCTAssertTrue(vm.laterRows.isEmpty)
        XCTAssertEqual(vm.state(of: .active).loadedValue?.count, 0)
    }

    func testOnlyAnInProgressJobOnActiveShowsTheHeroNotTheEmptyState() async {
        client.pagedResult = .success([.sample(id: "mine", status: ._4)])
        let vm = makeVM()
        await vm.selectTab(.active)
        XCTAssertEqual(vm.hero?.id, "mine")
        XCTAssertEqual(vm.currentState.loadedValue?.count, 1)
    }

    func testOpenDetailEmitsNavigateEffect() {
        let vm = makeVM()
        var captured: String?
        let token = vm.navigateToDetail.sink { captured = $0 }
        defer { token.cancel() }
        vm.openDetail("o1")
        XCTAssertEqual(captured, "o1")
    }
}
