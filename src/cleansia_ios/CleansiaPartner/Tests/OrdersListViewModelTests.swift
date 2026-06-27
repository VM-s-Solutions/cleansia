import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class OrdersListViewModelTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var staleness: OrdersStaleness!
    private var snackbar: SnackbarController!
    private var clock = Date(timeIntervalSince1970: 1000)

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        staleness = OrdersStaleness(window: 30, now: { self.clock })
        snackbar = SnackbarController()
    }

    private func makeVM() -> OrdersListViewModel {
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

    // MARK: inline actions (the shared machine)

    func testAvailableInlineActionIsTake() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        let vm = makeVM()
        await vm.onAppear()
        XCTAssertEqual(vm.inlineAction(for: .sample(id: "o1", status: ._2)), .take)
    }

    func testActiveInlineActionsByStatus() async {
        let vm = makeVM()
        await vm.selectTab(.active)
        XCTAssertEqual(vm.inlineAction(for: .sample(id: "a", status: ._2)), .notifyOnTheWay)
        XCTAssertEqual(vm.inlineAction(for: .sample(id: "b", status: ._3)), .start)
        XCTAssertEqual(vm.inlineAction(for: .sample(id: "c", status: ._4)), .complete)
    }

    func testRunInlineTakeSendsCommandForRowIdAndInvalidatesPanes() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        let vm = makeVM()
        await vm.onAppear()
        for pane in OrdersPane.allCases {
            staleness.markPaneFresh(pane)
        }

        await vm.runInlineAction(.take, on: .sample(id: "o1", status: ._2))

        XCTAssertEqual(client.commands.map(\.name), ["take"])
        XCTAssertEqual(client.commands.first?.orderId, "o1")
        // Take invalidates [available, active]; the current pane (available) is
        // then refetched (fresh again), so only the OTHER affected pane (active)
        // stays stale; history is untouched.
        XCTAssertFalse(staleness.isPaneStale(.available))
        XCTAssertTrue(staleness.isPaneStale(.active))
        XCTAssertFalse(staleness.isPaneStale(.history))
        XCTAssertNil(vm.inFlightActionOrderId)
    }

    func testRunInlineCompleteInvalidatesActiveAndHistory() async {
        client.employeeIdResult = .success("emp-self")
        let vm = makeVM()
        await vm.selectTab(.active)
        for pane in OrdersPane.allCases {
            staleness.markPaneFresh(pane)
        }

        await vm.runInlineAction(.complete, on: .sample(id: "o1", status: ._4))

        // Complete invalidates [active, history]; the current pane (active) is
        // refetched (fresh again), so history stays stale; available untouched.
        XCTAssertFalse(staleness.isPaneStale(.active))
        XCTAssertTrue(staleness.isPaneStale(.history))
        XCTAssertFalse(staleness.isPaneStale(.available))
    }

    func testInlineActionPerRowInFlightThenClears() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        client.suspendCommands = true
        let vm = makeVM()
        await vm.onAppear()

        let task = Task { await vm.runInlineAction(.take, on: .sample(id: "o1", status: ._2)) }
        while client.commands.isEmpty {
            await Task.yield()
        }
        XCTAssertEqual(vm.inFlightActionOrderId, "o1")

        client.resumeCommand()
        await task.value
        XCTAssertNil(vm.inFlightActionOrderId)
    }

    func testInlineActionReentryGuardDropsSecond() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        client.suspendCommands = true
        let vm = makeVM()
        await vm.onAppear()

        let first = Task { await vm.runInlineAction(.take, on: .sample(id: "o1", status: ._2)) }
        while client.commands.isEmpty {
            await Task.yield()
        }
        await vm.runInlineAction(.take, on: .sample(id: "o2", status: ._2)) // dropped
        XCTAssertEqual(client.commands.count, 1)

        client.resumeCommand()
        await first.value
    }

    func testInlineActionFailureSnackbarsAndRefreshes() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        let vm = makeVM()
        await vm.onAppear()
        client.commandResult = .failure(ApiError(httpStatus: 409)) // already-taken (O4)

        await vm.runInlineAction(.take, on: .sample(id: "o1", status: ._2))

        XCTAssertNotNil(snackbar.current)
        XCTAssertNil(vm.inFlightActionOrderId)
    }

    // MARK: TC-IOS-ORDERS-OWNERSHIP (O1 / O2)

    func testInlineActionActsOnlyOnRowIdNoEmployeeId() async {
        client.pagedResult = .success([.sample(id: "row-id", status: ._2)])
        let vm = makeVM()
        await vm.onAppear()

        await vm.runInlineAction(.take, on: .sample(id: "row-id", status: ._2))

        // O1: the command surface carries only orderId. O2: the carried id is
        // the row's own id from the list response.
        XCTAssertEqual(client.commands.first?.orderId, "row-id")
    }
}
