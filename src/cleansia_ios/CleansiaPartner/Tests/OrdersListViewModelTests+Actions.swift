import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

/// The Active hero's slide: set off, start, and nothing else — completing is the detail's.
@MainActor
extension OrdersListViewModelTests {
    func testTheHerosSlideSetsOffAConfirmedJobOnItsOwnId() async {
        client.pagedResult = .success([.sample(id: "row-id", status: ._2)])
        let vm = makeVM()
        await vm.selectTab(.active)

        await vm.advance(.sample(id: "row-id", status: ._2))

        XCTAssertEqual(client.commands.map(\.name), ["notifyOnTheWay"])
        XCTAssertEqual(client.commands.first?.orderId, "row-id")
        XCTAssertEqual(snackbar.current?.severity, .success)
        XCTAssertEqual(snackbar.current?.text, L10n.Orders.customerNotifiedOnTheWay)
    }

    func testTheHerosSlideStartsAJobOnTheWay() async {
        let vm = makeVM()
        await vm.selectTab(.active)

        await vm.advance(.sample(id: "o1", status: ._3))

        XCTAssertEqual(client.commands.map(\.name), ["start"])
        XCTAssertEqual(snackbar.current?.text, L10n.Orders.orderStartedToast)
    }

    /// In progress is *Open job*, never a list Complete (banner.md §4).
    func testAJobInProgressHasNoSlideAndTheBoardNeverCompletes() async {
        let vm = makeVM()
        await vm.selectTab(.active)

        await vm.advance(.sample(id: "o1", status: ._4))

        XCTAssertTrue(client.commands.isEmpty)
        XCTAssertEqual(OrdersBoardRules.heroControl(.sample(id: "o1", status: ._4), now: Date()), .openJob)
    }

    func testTheSlideStaysBusyThroughTheRefreshAndThenClears() async {
        client.pagedResult = .success([.sample(id: "o1", status: ._2)])
        let vm = makeVM()
        await vm.selectTab(.active)
        var inFlightDuringRefresh: String?
        client.onGetPaged = { inFlightDuringRefresh = vm.inFlightActionOrderId }

        await vm.advance(.sample(id: "o1", status: ._2))

        XCTAssertEqual(inFlightDuringRefresh, "o1")
        XCTAssertNil(vm.inFlightActionOrderId)
        XCTAssertTrue(staleness.isPaneStale(.available), "setting off changes the board's counts too")
    }

    func testASecondSlideWhileOneIsInFlightIsDropped() async {
        client.suspendCommands = true
        let vm = makeVM()
        await vm.selectTab(.active)
        let first = Task { await vm.advance(.sample(id: "o1", status: ._3)) }
        while client.commands.isEmpty {
            await Task.yield()
        }
        await vm.advance(.sample(id: "o2", status: ._3))
        XCTAssertEqual(client.commands.count, 1)
        client.resumeCommand()
        await first.value
    }

    func testARefusedSlideSnackbarsTheReasonAndClears() async {
        let vm = makeVM()
        await vm.selectTab(.active)
        client.commandResult = .failure(ApiError(httpStatus: 409))

        await vm.advance(.sample(id: "o1", status: ._3))

        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertNil(vm.inFlightActionOrderId)
    }
}
