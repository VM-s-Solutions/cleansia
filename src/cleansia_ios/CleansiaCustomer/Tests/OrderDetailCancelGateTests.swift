import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

private struct NoopLiveActivitySync: OrderLiveActivitySyncing {
    func start(orderId _: String, orderNumber _: String, status _: String, window _: EtaWindow) {}
    func update(orderId _: String, orderNumber _: String, status _: String, window _: EtaWindow) {}
    func end(orderId _: String, orderNumber _: String, status _: LiveActivityTerminalStatus) {}
}

/// The signed-in cancel affordance follows the server's own set — the statuses
/// `CancellationAssessor.BlockedReason` does not refuse — and the figure it confirms is the refund the
/// server actually issued, not the policy figure the preview quoted.
@MainActor
final class OrderDetailCancelGateTests: XCTestCase {
    private let snackbar = SnackbarController()

    private func makeVM(client: FakeOrderClient) -> OrderDetailViewModel {
        OrderDetailViewModel(
            orderId: "o1",
            client: client,
            repository: OrderRepository(client: client),
            membershipRepository: MembershipRepository(client: FakeMembershipManagementClient()),
            marketStore: MarketFixtures.store().0,
            snackbar: snackbar,
            eventBus: OrderEventBus(),
            liveActivity: NoopLiveActivitySync(),
            pollInterval: 60
        )
    }

    private func order(statusValue: Int) -> CustomerOrderDetail {
        OrderFixtures.detail(
            statusCode: Code(type: "OrderStatus", name: nil, value: statusValue),
            currencyCode: "CZK"
        )
    }

    private func canCancel(whenStatusIs statusValue: Int) async -> Bool {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: statusValue))]
        let vm = makeVM(client: client)
        await vm.load()
        return vm.canCancel
    }

    private func receipt(refundInitiated: Bool, actualRefundAmount: Double?) -> OrderCancellation {
        OrderCancellation(refundAmount: 750, refundInitiated: refundInitiated, actualRefundAmount: actualRefundAmount)
    }

    // MARK: - status gating

    /// Wire values: New=0, Pending=1, Confirmed=2, OnTheWay=3, InProgress=4, Completed=5, Cancelled=6.
    func testTheAffordanceIsOfferedInEveryStatusTheServerAllows() async {
        for status in [0, 1, 2, 3] {
            let offered = await canCancel(whenStatusIs: status)
            XCTAssertTrue(offered, "status \(status)")
        }
    }

    func testTheAffordanceIsWithheldOnceWorkHasStartedOrTheOrderIsClosed() async {
        for status in [4, 5, 6, 99] {
            let offered = await canCancel(whenStatusIs: status)
            XCTAssertFalse(offered, "status \(status)")
        }
    }

    func testNothingIsCancellableBeforeTheOrderHasLoaded() async {
        let client = FakeOrderClient()
        client.detailResults = [.failure(ApiError(httpStatus: 500))]
        let vm = makeVM(client: client)

        XCTAssertFalse(vm.canCancel)
        await vm.load()
        XCTAssertFalse(vm.canCancel)
    }

    /// The one function both the signed-in and the guest surface read, so the two cannot drift.
    func testTheGuestSurfaceReadsTheSameGate() {
        for status in 0 ... 6 {
            let guest = GuestOrderFixtures.order(statusValue: status)
            XCTAssertEqual(
                guest.isCancellable,
                OrderStatusGroup.isCancellable(OrderStatus(rawValue: status)),
                "status \(status)"
            )
        }
        XCTAssertEqual(
            (0 ... 6).filter { OrderStatusGroup.isCancellable(OrderStatus(rawValue: $0)) },
            [0, 1, 2, 3]
        )
    }

    /// The gate is a pure function the view binds through the view model. A status list re-inlined in
    /// the view would compile, and every test above would stay green while the two flows drifted apart
    /// again — so the binding is pinned by reading the one line.
    func testTheViewReadsTheGateFromTheViewModelRatherThanAStatusListOfItsOwn() throws {
        let source = try readSource("CleansiaCustomer/Sources/Features/Orders/OrderDetailView.swift")
        XCTAssertTrue(source.contains("showCancel: vm.canCancel,"), "the footer no longer binds Cancel to vm.canCancel")
    }

    // MARK: - the confirmed figure

    func testTheSuccessMessageCarriesTheRefundTheServerIssuedAndNeverThePolicyFigure() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 2)), .success(order(statusValue: 6))]
        client.cancelResult = .success(receipt(refundInitiated: true, actualRefundAmount: 12))
        let vm = makeVM(client: client)
        await vm.load()

        await vm.cancel(reason: "schedule_changed")

        let expected = L10n.OrderCancel.successWithRefund(OrdersFormat.price(12, currencyCode: "CZK"))
        XCTAssertEqual(snackbar.current?.text, expected)
        XCTAssertEqual(snackbar.current?.severity, .success)
        XCTAssertFalse(snackbar.current?.text.contains(OrdersFormat.price(750, currencyCode: "CZK")) ?? true)
    }

    func testACancelThatIssuedNoRefundUsesThePlainCopyEvenWhenThePolicyQuotedOne() async {
        for answer in [
            receipt(refundInitiated: false, actualRefundAmount: nil),
            receipt(refundInitiated: true, actualRefundAmount: nil),
            receipt(refundInitiated: true, actualRefundAmount: 0)
        ] {
            let client = FakeOrderClient()
            client.detailResults = [.success(order(statusValue: 2)), .success(order(statusValue: 6))]
            client.cancelResult = .success(answer)
            let vm = makeVM(client: client)
            await vm.load()

            await vm.cancel(reason: "schedule_changed")

            XCTAssertEqual(snackbar.current?.text, L10n.OrderCancel.successNoRefund, "\(answer)")
        }
    }

    // MARK: - the reason cap

    func testTheCapIsTheServerValidatorsFigure() {
        XCTAssertEqual(CancelReasonLimit.maxLength, 500)
    }

    func testTheNotesLimitLeavesRoomForTheReasonCodeAndItsSeparator() {
        XCTAssertEqual(CancelReasonLimit.notesLimit(reasonCode: "no_longer_needed"), 500 - "no_longer_needed".count - 2)
        XCTAssertEqual(CancelReasonLimit.notesLimit(reasonCode: nil), 500)
        XCTAssertEqual(CancelReasonLimit.notesLimit(reasonCode: String(repeating: "x", count: 600)), 0)
    }

    /// The sheet is the only place the limit is applied, and a private figure re-inlined there would
    /// compile with both tests above still green — so its one clip source is pinned the same way the
    /// view's gate binding is, and neither surface hands the sheet a figure of its own.
    func testTheSheetClipsTheNotesThroughTheSharedLimitAndCarriesNoFigureOfItsOwn() throws {
        let sheet = try readSource("CleansiaCustomer/Sources/Features/Orders/CancelOrderSheet.swift")
        XCTAssertTrue(sheet.contains("CancelReasonLimit.notesLimit(reasonCode: selectedReason?.code)"))
        XCTAssertFalse(sheet.contains("2000"), "the sheet carries a notes limit of its own")
        XCTAssertFalse(sheet.contains("reasonLimit"), "the sheet takes a per-caller limit again")

        for view in ["OrderDetailView.swift", "GuestOrderView.swift"] {
            let source = try readSource("CleansiaCustomer/Sources/Features/Orders/\(view)")
            XCTAssertFalse(source.contains("reasonLimit"), "\(view) hands the sheet a limit of its own")
        }
    }

    private func readSource(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
