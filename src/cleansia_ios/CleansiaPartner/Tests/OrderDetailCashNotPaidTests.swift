import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The cleaner's other answer to "did the customer pay?": nothing was paid at the door, so the job completes and
/// the price becomes the customer's debt.
@MainActor
final class OrderDetailCashNotPaidTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var staleness: OrdersStaleness!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        staleness = OrdersStaleness()
        snackbar = SnackbarController()
    }

    private func makeVM() -> OrderDetailViewModel {
        OrderDetailViewModel(
            orderId: "order-1",
            client: client,
            staleness: staleness,
            snackbar: snackbar,
            pendingOffers: PendingOffersStore(client: client, ordersStaleness: staleness)
        )
    }

    private func cashJob(
        status: Int = 4,
        isMine: Bool = true,
        hasAfterPhotos: Bool = true,
        paymentType: Int = 1,
        paymentStatus: Int = 1
    ) -> OrderItem {
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: status)
        item.isAssignedToCurrentUser = isMine
        item.hasAfterPhotos = hasAfterPhotos
        item.paymentType = Code(value: paymentType)
        item.paymentStatus = Code(value: paymentStatus)
        return item
    }

    private func loaded(_ item: OrderItem) async -> OrderDetailViewModel {
        client.byIdResult = .success(item)
        let vm = makeVM()
        await vm.load()
        return vm
    }

    func testOfferedOnlyWhereTheCashIsCollected() async {
        let cases: [(item: OrderItem, offered: Bool, why: String)] = [
            (cashJob(), true, "my cash job, in progress, after photo taken, payment pending"),
            (cashJob(paymentType: 2), false, "a card job"),
            (cashJob(paymentStatus: 2), false, "the cash is already collected"),
            (cashJob(paymentStatus: 4), false, "a refunded payment has nothing outstanding"),
            (cashJob(status: 3), false, "not started yet"),
            (cashJob(status: 5), false, "already completed"),
            (cashJob(isMine: false), false, "someone else's job"),
            (cashJob(hasAfterPhotos: false), false, "no after photo yet, which the server requires")
        ]
        for testCase in cases {
            let vm = await loaded(testCase.item)
            XCTAssertEqual(vm.offersCashNotPaid, testCase.offered, testCase.why)
        }
    }

    func testNotOfferedBeforeTheOrderLoads() {
        XCTAssertFalse(makeVM().offersCashNotPaid)
    }

    /// The confirmation states what the customer will owe: the price less any credit applied.
    func testTheAmountOwedIsThePriceLessTheCreditApplied() throws {
        var item = cashJob()
        item.totalPrice = 1200
        item.creditAppliedAmount = 300

        let order = try OrderDetail(item)

        XCTAssertEqual(order.cashNotPaidOwedLabel, OrdersFormat.money(900, symbol: order.currencySymbol))
    }

    func testAMissingPriceOrCreditFigureNamesNoAmountRatherThanAGuessedOne() throws {
        var noCredit = cashJob()
        noCredit.totalPrice = 1200
        noCredit.creditAppliedAmount = nil
        var noPrice = cashJob()
        noPrice.totalPrice = nil
        noPrice.creditAppliedAmount = 0

        XCTAssertNil(try OrderDetail(noCredit).cashNotPaidOwedLabel)
        XCTAssertNil(try OrderDetail(noPrice).cashNotPaidOwedLabel)
    }

    /// The report alerts an administrator, and an administrator can reverse it (a manager writes the debt off, or the
    /// cash is recorded as received), so "cannot be undone" overstates it. The phrases are partner web's own, so all
    /// three partner clients state the same consequences. Read through the BUILT bundle, in every shipped language.
    func testTheConfirmSaysAnAdministratorIsAlertedAndOnlyAnAdministratorCanUndoIt() throws {
        let cases: [(language: String, promised: [String], retired: String)] = [
            ("en", ["an administrator is alerted", "Only an administrator can undo this."], "cannot be undone"),
            ("cs", ["administrátor dostane upozornění", "Vrátit to může jen administrátor."], "nelze vzít zpět"),
            ("sk", ["administrátor dostane upozornenie", "Vrátiť to môže len administrátor."], "nemožno vrátiť späť"),
            ("uk", ["адміністратор отримає сповіщення", "Скасувати це може лише адміністратор."], "не можна скасувати"),
            ("ru", ["администратор получит уведомление", "Отменить это может только администратор."], "нельзя отменить")
        ]
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for testCase in cases {
            L10n.bundle = try localeBundle(testCase.language)
            let messages = [
                L10n.Orders.cashNotPaidConfirmMessage("900"),
                L10n.Orders.cashNotPaidConfirmMessageNoAmount
            ]
            for message in messages {
                let language = testCase.language
                for phrase in testCase.promised {
                    XCTAssertTrue(message.contains(phrase), "\(language) omits \"\(phrase)\": \(message)")
                }
                XCTAssertFalse(message.contains(testCase.retired), "\(language) says it is final: \(message)")
            }
        }
    }

    func testTheReportSendsOnlyTheOrderIdConfirmsAndRefetches() async {
        let vm = await loaded(cashJob())
        let fetchesBefore = client.getByIdCallCount

        await vm.reportCashNotPaid()

        XCTAssertEqual(client.commands.map(\.name), ["reportCashNotPaid"])
        XCTAssertEqual(client.commands.map(\.orderId), ["order-1"])
        XCTAssertEqual(snackbar.current?.severity, .success)
        XCTAssertEqual(snackbar.current?.text, L10n.Orders.cashNotPaidReportedToast)
        XCTAssertEqual(client.getByIdCallCount, fetchesBefore + 1)
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.inFlightAction)
    }

    /// The report completes the job, so it leaves the active list for the history, as a completion does.
    func testTheReportMovesTheJobFromActiveToHistory() async {
        let vm = await loaded(cashJob())
        for pane in OrdersPane.allCases {
            staleness.markPaneFresh(pane)
        }

        await vm.reportCashNotPaid()

        XCTAssertTrue(staleness.isPaneStale(.active))
        XCTAssertTrue(staleness.isPaneStale(.history))
        XCTAssertFalse(staleness.isPaneStale(.available))
    }

    func testTheReportHoldsItsOwnInFlightDiscriminator() async {
        let vm = await loaded(cashJob())
        client.suspendCommands = true

        let reporting = Task { await vm.reportCashNotPaid() }
        while client.commands.isEmpty {
            await Task.yield()
        }
        XCTAssertEqual(vm.inFlightAction, .reportCashNotPaid)
        XCTAssertTrue(vm.actionState.isSubmitting)

        client.resumeCommand()
        await reporting.value
        XCTAssertNil(vm.inFlightAction)
    }

    func testARefusedReportSnackbarsTheReasonAndConfirmsNothing() async {
        let vm = await loaded(cashJob())
        client.commandResult = .failure(ApiError(code: "order.cash_already_collected", httpStatus: 400))

        await vm.reportCashNotPaid()

        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertNotEqual(snackbar.current?.text, L10n.Orders.cashNotPaidReportedToast)
        guard case .error = vm.actionState else { return XCTFail("expected action error") }
        XCTAssertNil(vm.inFlightAction)
    }

    /// "Cash collected" and "did not pay" answer the same question, so the second is dropped while the first
    /// is on its way.
    func testTheReportIsDroppedWhileTheCashCollectionIsInFlight() async {
        let vm = await loaded(cashJob())
        client.suspendCommands = true

        let collecting = Task { await vm.markCashCollected() }
        while client.commands.isEmpty {
            await Task.yield()
        }
        await vm.reportCashNotPaid()
        XCTAssertEqual(client.commands.map(\.name), ["markCashCollected"])

        client.resumeCommand()
        await collecting.value
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
