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

    private func makeVM(
        client: FakeOrderClient,
        now: @escaping () -> Date = Date.init
    ) -> OrderDetailViewModel {
        OrderDetailViewModel(
            orderId: "o1",
            client: client,
            repository: OrderRepository(client: client),
            membershipRepository: MembershipRepository(client: FakeMembershipManagementClient()),
            marketStore: MarketFixtures.store().0,
            snackbar: snackbar,
            eventBus: OrderEventBus(),
            liveActivity: NoopLiveActivitySync(),
            pollInterval: 60,
            now: now
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

    func testOnlyTheFourPreStartStatusesAreCancellable() {
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

    // MARK: - past the booked start

    private let start = Date(timeIntervalSince1970: 1_800_000_000)

    private func order(statusValue: Int, staffed: Bool) -> CustomerOrderDetail {
        OrderFixtures.detail(
            statusCode: Code(type: "OrderStatus", name: nil, value: statusValue),
            cleaningDateTime: start,
            currencyCode: "CZK",
            assignedEmployees: staffed
                ? [AssignedEmployeeDto(id: "seat-1", employeeId: "emp-1", fullName: "Jana", phoneNumber: nil)]
                : []
        )
    }

    private func loadedVM(_ order: CustomerOrderDetail, at instant: Date) async -> OrderDetailViewModel {
        let client = FakeOrderClient()
        client.detailResults = [.success(order)]
        let vm = makeVM(client: client, now: { instant })
        await vm.load()
        return vm
    }

    /// `CancellationAssessor.BlockedReason` refuses a staffed order once its start has passed, whatever
    /// the not-started status, and the customer reports the no-show instead of paying the last-minute fee.
    func testPastTheStartAStaffedOrderOffersTheNoShowReportInCancelsPlace() async {
        for status in [0, 1, 2, 3] {
            let vm = await loadedVM(order(statusValue: status, staffed: true), at: start.addingTimeInterval(60))
            XCTAssertFalse(vm.canCancel, "status \(status)")
            XCTAssertTrue(vm.canReportCleanerNoShow, "status \(status)")
        }
    }

    /// The server compares `nowUtc >= CleaningDateTime`: the start itself is already past it.
    func testTheBookedStartItselfIsWhereCancelGivesWay() async {
        let atStart = await loadedVM(order(statusValue: 2, staffed: true), at: start)
        XCTAssertFalse(atStart.canCancel)
        XCTAssertTrue(atStart.canReportCleanerNoShow)

        let justBefore = await loadedVM(order(statusValue: 3, staffed: true), at: start.addingTimeInterval(-1))
        XCTAssertTrue(justBefore.canCancel)
        XCTAssertFalse(justBefore.canReportCleanerNoShow)
    }

    /// Nobody took the job, so nobody failed to arrive: the unfilled sweep owns that order, and the
    /// customer may still cancel it free.
    func testAnUnstaffedOrderPastItsStartStaysCancellable() async {
        let vm = await loadedVM(order(statusValue: 0, staffed: false), at: start.addingTimeInterval(3600))
        XCTAssertTrue(vm.canCancel)
        XCTAssertFalse(vm.canReportCleanerNoShow)
    }

    func testOnceWorkHasStartedOrTheOrderIsClosedNeitherIsOffered() async {
        for status in [4, 5, 6] {
            let vm = await loadedVM(order(statusValue: status, staffed: true), at: start.addingTimeInterval(3600))
            XCTAssertFalse(vm.canCancel, "status \(status)")
            XCTAssertFalse(vm.canReportCleanerNoShow, "status \(status)")
        }
    }

    func testAnOrderWithNoStartTimeKeepsCancel() async {
        let undated = OrderFixtures.detail(
            statusCode: Code(type: "OrderStatus", name: nil, value: 2),
            assignedEmployees: [
                AssignedEmployeeDto(id: "seat-1", employeeId: "emp-1", fullName: "Jana", phoneNumber: nil)
            ]
        )
        let vm = await loadedVM(undated, at: start)
        XCTAssertTrue(vm.canCancel)
        XCTAssertFalse(vm.canReportCleanerNoShow)
    }

    /// The gate reads the clock when asked, not at load: a screen left open across the start swaps the
    /// footer on the poller's next re-render without a fetch deciding it.
    func testTheClockIsReadAtEveryAskNotFrozenAtLoad() async {
        var clock = start.addingTimeInterval(-60)
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 2, staffed: true))]
        let vm = makeVM(client: client, now: { clock })
        await vm.load()
        XCTAssertTrue(vm.canCancel)

        clock = start.addingTimeInterval(60)

        XCTAssertFalse(vm.canCancel)
        XCTAssertTrue(vm.canReportCleanerNoShow)
        XCTAssertEqual(client.detailCallCount, 1)
    }

    func testNothingIsReportedBeforeTheOrderHasLoaded() {
        let late = start.addingTimeInterval(3600)
        let vm = makeVM(client: FakeOrderClient(), now: { late })
        XCTAssertFalse(vm.canReportCleanerNoShow)
    }

    func testTheViewOffersTheReportFromTheViewModelAndTheShellFilesItAsServiceNotProvided() throws {
        let view = try readSource("CleansiaCustomer/Sources/Features/Orders/OrderDetailView.swift")
        XCTAssertTrue(view.contains("showCleanerDidNotArrive: vm.canReportCleanerNoShow,"))
        XCTAssertTrue(view.contains("onCleanerDidNotArrive: { onReportCleanerNoShow(orderId) },"))

        let shell = try readSource("CleansiaCustomer/Sources/Features/Shell/CustomerShellView.swift")
        XCTAssertTrue(shell.contains("reason: DisputeReasonOption.serviceNotProvided"))
        XCTAssertTrue(shell.contains("initialReason: reason,"))
    }

    /// `DisputeReason.ServiceNotProvided = 2` on the server; the form's picker offers the same value.
    func testTheNoShowReasonIsTheServersServiceNotProvided() {
        XCTAssertEqual(DisputeReasonOption.serviceNotProvided, 2)
        XCTAssertTrue(DisputeReasonOption.all.contains { $0.value == DisputeReasonOption.serviceNotProvided })
        XCTAssertEqual(
            L10n.Disputes.reason(DisputeReasonOption.serviceNotProvided),
            L10n.localized("dispute_reason_service_not_provided")
        )
    }

    // MARK: - no card payment

    private func tookNoCardPayment(paymentType: Int, paymentStatus: Int) async -> Bool {
        let detail = OrderFixtures.detail(
            statusCode: Code(type: "OrderStatus", name: nil, value: 2),
            paymentType: Code(type: "PaymentType", name: nil, value: paymentType),
            paymentStatus: Code(type: "PaymentStatus", name: nil, value: paymentStatus),
            currencyCode: "CZK"
        )
        let client = FakeOrderClient()
        client.detailResults = [.success(detail)]
        let vm = makeVM(client: client)
        await vm.load()
        return vm.tookNoCardPayment
    }

    /// Wire values: PaymentType Cash=1, Card=2; PaymentStatus Pending=1, Paid=2, Failed=3,
    /// PartiallyRefunded=6. The server's `Order.TookNoPayment` is Pending or Failed; a confirmed recurring
    /// cash occurrence rests at Paid with nothing taken, so cash is read off the type as well.
    func testTheSheetStatesNoRefundOnCashOrOnACardThatWasNeverCharged() async {
        let cashPending = await tookNoCardPayment(paymentType: 1, paymentStatus: 1)
        let cashPaid = await tookNoCardPayment(paymentType: 1, paymentStatus: 2)
        let cardPending = await tookNoCardPayment(paymentType: 2, paymentStatus: 1)
        let cardFailed = await tookNoCardPayment(paymentType: 2, paymentStatus: 3)
        XCTAssertTrue(cashPending)
        XCTAssertTrue(cashPaid)
        XCTAssertTrue(cardPending)
        XCTAssertTrue(cardFailed)
    }

    func testACardThatWasChargedKeepsTheRefundLine() async {
        let cardPaid = await tookNoCardPayment(paymentType: 2, paymentStatus: 2)
        let cardPartlyRefunded = await tookNoCardPayment(paymentType: 2, paymentStatus: 6)
        XCTAssertFalse(cardPaid)
        XCTAssertFalse(cardPartlyRefunded)
    }

    func testNoPaymentFactsAreReadBeforeTheOrderHasLoaded() {
        XCTAssertFalse(makeVM(client: FakeOrderClient()).tookNoCardPayment)
    }

    func testTheViewHandsTheSheetTheViewModelsReading() throws {
        let view = try readSource("CleansiaCustomer/Sources/Features/Orders/OrderDetailView.swift")
        XCTAssertTrue(view.contains("tookNoCardPayment: vm.tookNoCardPayment"))
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

    /// The boundaries below are written in astral-plane text: `MaximumLength(500)` measures .NET
    /// `string.Length` — UTF-16 units — and an ASCII-only suite passes under `count` too and proves nothing.
    private let emoji = "😀"

    func testTheCapIsTheServerValidatorsFigure() {
        XCTAssertEqual(CancelReasonLimit.maxUtf16Length, 500)
    }

    func testTheNotesLimitLeavesRoomForTheReasonCodeAndItsSeparatorInUtf16Units() {
        let code = "no_longer_needed"
        XCTAssertEqual(CancelReasonLimit.notesLimit(reasonCode: code), 500 - code.utf16.count - 2)
        XCTAssertEqual(CancelReasonLimit.notesLimit(reasonCode: emoji), 500 - 2 - 2)
        XCTAssertEqual(CancelReasonLimit.notesLimit(reasonCode: nil), 500)
        XCTAssertEqual(CancelReasonLimit.notesLimit(reasonCode: String(repeating: "x", count: 600)), 0)
    }

    func testNotesWithinTheBudgetAreReturnedUnchanged() {
        XCTAssertEqual(CancelReasonLimit.cappedNotes("Plans changed", reasonCode: "other"), "Plans changed")
    }

    /// 300 emoji behind "other" read 305 to `String.count` — under the cap — and 607 to the server. The
    /// budget of 493 falls one unit inside the 247th emoji, so that one must be dropped whole, not halved.
    func testNotesAreClippedInUtf16UnitsNotGraphemesAndNeverSplitASurrogatePair() {
        let notes = String(repeating: emoji, count: 300)
        XCTAssertEqual(notes.count, 300)
        XCTAssertEqual(notes.utf16.count, 600)

        let capped = CancelReasonLimit.cappedNotes(notes, reasonCode: "other")

        XCTAssertLessThanOrEqual(("other: " + capped).utf16.count, CancelReasonLimit.maxUtf16Length)
        XCTAssertEqual(capped.count, 246)
        XCTAssertFalse(capped.unicodeScalars.contains("\u{FFFD}"))
    }

    /// The sheet is the only place the limit is applied, and a private figure re-inlined there would
    /// compile with the tests above still green — so its one clip source is pinned the same way the
    /// view's gate binding is, and neither surface hands the sheet a figure of its own.
    func testTheSheetClipsTheNotesThroughTheSharedLimitAndCarriesNoFigureOfItsOwn() throws {
        let sheet = try readSource("CleansiaCustomer/Sources/Features/Orders/CancelOrderSheet.swift")
        XCTAssertTrue(sheet.contains("CancelReasonLimit.cappedNotes("))
        XCTAssertFalse(sheet.contains(".prefix("), "the sheet clips by grapheme again")
        XCTAssertFalse(sheet.contains("2000"), "the sheet carries a notes limit of its own")
        XCTAssertFalse(sheet.contains("reasonLimit"), "the sheet takes a per-caller limit again")

        let detail = try readSource("CleansiaCustomer/Sources/Features/Orders/OrderDetailView.swift")
        XCTAssertFalse(detail.contains("reasonLimit"), "OrderDetailView hands the sheet a limit of its own")
    }

    private func readSource(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
