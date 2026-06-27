import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class OrderDetailViewModelTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var staleness: OrdersStaleness!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        staleness = OrdersStaleness()
        snackbar = SnackbarController()
    }

    private func makeVM(orderId: String = "order-1") -> OrderDetailViewModel {
        OrderDetailViewModel(orderId: orderId, client: client, staleness: staleness, snackbar: snackbar)
    }

    private func loadedItem(
        id: String = "order-1",
        status: Int = 4,
        isMine: Bool = true,
        hasAfterPhotos: Bool = true
    ) -> OrderItem {
        var item = OrderItem()
        item.id = id
        item.displayOrderNumber = "ORD-1"
        item.orderStatus = Code(value: status)
        item.address = OrderAddress(latitude: 50.0, longitude: 14.0)
        item.isAssignedToCurrentUser = isMine
        item.hasAfterPhotos = hasAfterPhotos
        return item
    }

    func testInitialStateIsLoading() {
        XCTAssertTrue(makeVM().state.isLoading)
    }

    func testActionStateAndInFlightStartIdle() {
        let vm = makeVM()
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.inFlightAction)
    }

    func testLoadTransitionsLoadingToLoaded() async {
        client.byIdResult = .success(loadedItem())
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue?.id, "order-1")
        XCTAssertEqual(vm.state.loadedValue?.status, ._4)
    }

    func testLoadFailureWithNoCacheTransitionsToErrorAndSnackbars() async {
        client.byIdResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeVM()
        await vm.load()
        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testBackgroundRefetchErrorStaysLoaded() async {
        client.byIdResult = .success(loadedItem())
        let vm = makeVM()
        await vm.load()
        XCTAssertNotNil(vm.state.loadedValue)

        // A subsequent failed refetch must NOT flip a loaded order to .error
        // (OrderDetailViewModel.kt:74-79 parity) — it only snackbars.
        client.byIdResult = .failure(ApiError(httpStatus: 500))
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue?.id, "order-1")
        XCTAssertNotNil(snackbar.current)
    }

    func testCanShowMapHidesOnlyOnCancelled() async {
        client.byIdResult = .success(loadedItem(status: 5)) // Completed → still shows
        let completedVM = makeVM()
        await completedVM.load()
        XCTAssertEqual(completedVM.state.loadedValue?.canShowMap, true)

        client.byIdResult = .success(loadedItem(status: 6)) // Cancelled → hides
        let cancelledVM = makeVM()
        await cancelledVM.load()
        XCTAssertEqual(cancelledVM.state.loadedValue?.canShowMap, false)
    }

    func testCanShowMapFalseWhenNoCoordinate() async {
        var item = loadedItem()
        item.address = OrderAddress(street: "X")
        client.byIdResult = .success(item)
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue?.canShowMap, false)
    }

    // MARK: primaryAction via the shared machine

    func testPrimaryActionResolvesViaMachine() async {
        client.byIdResult = .success(loadedItem(status: 4, isMine: true, hasAfterPhotos: true))
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.primaryAction, .complete)
    }

    func testPrimaryActionCompleteBlockedWithoutAfterPhotos() async {
        client.byIdResult = .success(loadedItem(status: 4, isMine: true, hasAfterPhotos: false))
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.primaryAction, .completeBlocked)
    }

    // TC-IOS-PHOTOS-GATE: after an after-photo upload, the photos VM fires
    // `mutated` → the parent re-fetches → the server-recomputed hasAfterPhotos
    // flips completeBlocked → complete.
    func testCompleteUnblocksAfterRefetchWithAfterPhotos() async {
        client.byIdResult = .success(loadedItem(status: 4, isMine: true, hasAfterPhotos: false))
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.primaryAction, .completeBlocked)

        client.byIdResult = .success(loadedItem(status: 4, isMine: true, hasAfterPhotos: true))
        await vm.load() // the re-fetch the photos `mutated` triggers
        XCTAssertEqual(vm.primaryAction, .complete)
    }

    // MARK: runAction lifecycle

    func testStartActionIdleToIdleOnSuccessAndRefetches() async {
        client.byIdResult = .success(loadedItem(status: 3))
        let vm = makeVM()
        await vm.load()
        let fetchesBefore = client.getByIdCallCount

        await vm.start()

        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.inFlightAction)
        XCTAssertEqual(client.commands.map(\.name), ["start"])
        XCTAssertEqual(client.getByIdCallCount, fetchesBefore + 1) // post-success refetch
    }

    func testActionSuccessInvalidatesTheMappedPanes() async {
        client.byIdResult = .success(loadedItem(status: 2))
        let vm = makeVM()
        await vm.load()
        for pane in OrdersPane.allCases {
            staleness.markPaneFresh(pane)
        }

        await vm.notifyOnTheWay() // → invalidates Available + Active

        XCTAssertTrue(staleness.isPaneStale(.available))
        XCTAssertTrue(staleness.isPaneStale(.active))
        XCTAssertFalse(staleness.isPaneStale(.history))
    }

    func testCompleteSuccessShowsSuccessSnackbar() async {
        client.byIdResult = .success(loadedItem(status: 4))
        let vm = makeVM()
        await vm.load()
        await vm.complete()
        XCTAssertNotNil(snackbar.current)
        XCTAssertEqual(vm.actionState, .idle)
    }

    func testActionFailureSurfacesErrorAndKeepsScreen() async {
        client.byIdResult = .success(loadedItem(status: 3))
        let vm = makeVM()
        await vm.load()

        client.commandResult = .failure(ApiError(httpStatus: 409)) // already-taken-style reject
        await vm.start()

        guard case .error = vm.actionState else { return XCTFail("expected action error") }
        XCTAssertNotNil(snackbar.current)
        XCTAssertNotNil(vm.state.loadedValue) // screen kept + refreshed (O4)
    }

    func testInFlightActionTracksTheRunningAction() async {
        client.byIdResult = .success(loadedItem(status: 3))
        client.suspendCommands = true
        let vm = makeVM()
        await vm.load()

        let task = Task { await vm.start() }
        while client.commands.isEmpty {
            await Task.yield()
        }
        XCTAssertEqual(vm.inFlightAction, .start)
        XCTAssertTrue(vm.actionState.isSubmitting)

        client.resumeCommand()
        await task.value
        XCTAssertNil(vm.inFlightAction)
    }

    func testReentryGuardDropsASecondActionWhileSubmitting() async {
        client.byIdResult = .success(loadedItem(status: 3))
        client.suspendCommands = true
        let vm = makeVM()
        await vm.load()

        let first = Task { await vm.start() }
        while client.commands.isEmpty {
            await Task.yield()
        }
        await vm.start() // second — must be dropped by the guard
        XCTAssertEqual(client.commands.count, 1)

        client.resumeCommand()
        await first.value
    }

    // MARK: TC-IOS-ORDERS-OWNERSHIP (O1 / O2)

    func testCommandsCarryOnlyTheLoadedOrderIdNoEmployeeId() async {
        // O1: the client surface for each command carries ONLY orderId (no
        // employeeId parameter exists) — the actor is the JWT, server-side.
        // O2: the carried id is the loaded/own order id, never synthesized.
        client.byIdResult = .success(loadedItem(id: "order-xyz", status: 2))
        let vm = makeVM(orderId: "order-xyz")
        await vm.load()

        await vm.notifyOnTheWay()

        XCTAssertEqual(client.commands.count, 1)
        XCTAssertEqual(client.commands.first?.orderId, "order-xyz")
        XCTAssertEqual(client.commands.first?.name, "notifyOnTheWay")
    }
}
