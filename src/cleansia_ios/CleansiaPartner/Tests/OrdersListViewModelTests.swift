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
}
