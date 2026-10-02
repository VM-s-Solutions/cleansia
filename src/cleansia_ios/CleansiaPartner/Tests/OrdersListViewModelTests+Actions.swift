import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
extension OrdersListViewModelTests {
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

    /// Taking is accepting the contract for work: the row's Take opens the sheet for that row's id and
    /// writes nothing itself — the take happens on the swipe inside the sheet.
    func testRunInlineTakeOpensTheContractSheetForTheRowAndWritesNothing() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        let vm = makeVM()
        await vm.onAppear()

        await vm.runInlineAction(.take, on: .sample(id: "o1", status: ._2))

        XCTAssertEqual(vm.contractRequest, .take(orderId: "o1"))
        XCTAssertTrue(client.commands.isEmpty)
        XCTAssertNil(vm.inFlightActionOrderId)
    }

    func testDismissingTheSheetClearsTheRequest() async {
        let vm = makeVM()
        await vm.runInlineAction(.take, on: .sample(id: "o1", status: ._2))

        vm.dismissContract()

        XCTAssertNil(vm.contractRequest)
    }

    func testATakeIsNotOfferedWhileAnotherRowIsInFlight() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._3)])
        client.suspendCommands = true
        let vm = makeVM()
        await vm.selectTab(.active)
        let first = Task { await vm.runInlineAction(.start, on: .sample(id: "o1", status: ._3)) }
        while client.commands.isEmpty {
            await Task.yield()
        }

        await vm.runInlineAction(.take, on: .sample(id: "o2", status: ._2))

        XCTAssertNil(vm.contractRequest)
        client.resumeCommand()
        await first.value
    }

    /// The sheet's verdict is reconciled exactly as the one-tap take was.
    func testATakenOutcomeInvalidatesThePanesAndRefreshesTheBoard() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        let vm = makeVM()
        await vm.onAppear()
        await vm.runInlineAction(.take, on: .sample(id: "o1", status: ._2))
        for pane in OrdersPane.allCases {
            staleness.markPaneFresh(pane)
        }
        let fetchesBefore = client.getPagedCallCount

        await vm.onWorkContractOutcome(.taken(orderId: "o1"))

        XCTAssertNil(vm.contractRequest)
        XCTAssertEqual(client.getPagedCallCount, fetchesBefore + 1)
        // Take invalidates [available, active]; the current pane (available) is
        // then refetched (fresh again), so only the OTHER affected pane (active)
        // stays stale; history is untouched.
        XCTAssertFalse(staleness.isPaneStale(.available))
        XCTAssertTrue(staleness.isPaneStale(.active))
        XCTAssertFalse(staleness.isPaneStale(.history))
        XCTAssertNil(vm.inFlightActionOrderId)
    }

    func testInFlightHeldThroughTheOutcomesRefresh() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        let vm = makeVM()
        await vm.onAppear()
        await vm.runInlineAction(.take, on: .sample(id: "o1", status: ._2))

        var inFlightDuringRefresh: String?
        client.onGetPaged = { inFlightDuringRefresh = vm.inFlightActionOrderId }
        await vm.onWorkContractOutcome(.taken(orderId: "o1"))

        XCTAssertEqual(inFlightDuringRefresh, "o1")
        XCTAssertNil(vm.inFlightActionOrderId)
    }

    func testARefusedTakeOutcomeSnackbarsAndRefreshesTheBoard() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        let vm = makeVM()
        await vm.onAppear()
        await vm.runInlineAction(.take, on: .sample(id: "o1", status: ._2))
        let fetchesBefore = client.getPagedCallCount

        await vm.onWorkContractOutcome(
            .refused(.take(orderId: "o1"), ApiError(code: "order.no_available_spots", httpStatus: 400))
        )

        XCTAssertNil(vm.contractRequest)
        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertEqual(client.getPagedCallCount, fetchesBefore + 1)
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
        client.pagedResult = .success([.sample(id: "o1", status: ._3)])
        client.suspendCommands = true
        let vm = makeVM()
        await vm.selectTab(.active)

        let task = Task { await vm.runInlineAction(.start, on: .sample(id: "o1", status: ._3)) }
        while client.commands.isEmpty {
            await Task.yield()
        }
        XCTAssertEqual(vm.inFlightActionOrderId, "o1")

        client.resumeCommand()
        await task.value
        XCTAssertNil(vm.inFlightActionOrderId)
    }

    func testInlineActionReentryGuardDropsSecond() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._3)])
        client.suspendCommands = true
        let vm = makeVM()
        await vm.selectTab(.active)

        let first = Task { await vm.runInlineAction(.start, on: .sample(id: "o1", status: ._3)) }
        while client.commands.isEmpty {
            await Task.yield()
        }
        await vm.runInlineAction(.start, on: .sample(id: "o2", status: ._3)) // dropped
        XCTAssertEqual(client.commands.count, 1)

        client.resumeCommand()
        await first.value
    }

    func testInlineActionFailureSnackbarsAndRefreshes() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._3)])
        let vm = makeVM()
        await vm.selectTab(.active)
        client.commandResult = .failure(ApiError(httpStatus: 409)) // already-started (O4)

        await vm.runInlineAction(.start, on: .sample(id: "o1", status: ._3))

        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertNil(vm.inFlightActionOrderId)
    }

    // MARK: slide-transition feedback — every successful list transition confirms

    func testNotifyOnTheWaySuccessShowsSuccessSnackbar() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        let vm = makeVM()
        await vm.selectTab(.active)

        await vm.runInlineAction(.notifyOnTheWay, on: .sample(id: "o1", status: ._2))

        XCTAssertEqual(snackbar.current?.severity, .success)
        XCTAssertEqual(snackbar.current?.text, L10n.Orders.customerNotifiedOnTheWay)
    }

    func testStartSuccessShowsSuccessSnackbar() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._3)])
        let vm = makeVM()
        await vm.selectTab(.active)

        await vm.runInlineAction(.start, on: .sample(id: "o1", status: ._3))

        XCTAssertEqual(snackbar.current?.severity, .success)
        XCTAssertEqual(snackbar.current?.text, L10n.Orders.orderStartedToast)
    }

    func testCompleteSuccessShowsSuccessSnackbar() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._4)])
        let vm = makeVM()
        await vm.selectTab(.active)

        await vm.runInlineAction(.complete, on: .sample(id: "o1", status: ._4))

        XCTAssertEqual(snackbar.current?.severity, .success)
        XCTAssertEqual(snackbar.current?.text, L10n.Orders.orderCompletedToast)
    }

    func testTakeSuccessStaysSilent() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        let vm = makeVM()
        await vm.onAppear()

        await vm.runInlineAction(.take, on: .sample(id: "o1", status: ._2))
        await vm.onWorkContractOutcome(.taken(orderId: "o1"))

        XCTAssertNil(snackbar.current)
    }

    func testInFlightHeldUntilSuccessRefreshCompletes() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        let vm = makeVM()
        await vm.selectTab(.active)

        var inFlightDuringRefresh: String?
        client.onGetPaged = { inFlightDuringRefresh = vm.inFlightActionOrderId }
        await vm.runInlineAction(.notifyOnTheWay, on: .sample(id: "o1", status: ._2))

        XCTAssertEqual(inFlightDuringRefresh, "o1")
        XCTAssertNil(vm.inFlightActionOrderId)
    }
}
