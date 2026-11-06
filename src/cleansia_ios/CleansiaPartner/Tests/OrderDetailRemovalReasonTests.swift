import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// A cleaner taken off a job by an administrator is told why: the removal notice opens the detail, and only that
/// entry asks the server for the written reason. `GetById` refuses her once she is off the job, so the page says it
/// in place of the job: no alert, nothing to dismiss.
@MainActor
final class OrderDetailRemovalReasonTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var staleness: OrdersStaleness!
    private var snackbar: SnackbarController!
    private var store: OrdersBoardStore!

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        staleness = OrdersStaleness()
        snackbar = SnackbarController()
        store = OrdersBoardStore()
    }

    private func makeVM(showRemovalReason: Bool, orderId: String = "order-1") -> OrderDetailViewModel {
        OrderDetailViewModel(
            orderId: orderId,
            client: client,
            staleness: staleness,
            snackbar: snackbar,
            pendingOffers: PendingOffersStore(client: client, ordersStaleness: staleness),
            boardStore: store,
            showRemovalReason: showRemovalReason
        )
    }

    private func refused() {
        client.byIdResult = .failure(ApiError(code: "order.not_found", httpStatus: 404))
    }

    func testOpenedFromTheRemovalNoticeTheAdministratorsReasonIsFetchedAndShown() async {
        client.removalReasonResult = .success("The customer asked for another cleaner.")
        let vm = makeVM(showRemovalReason: true)

        await vm.load()

        XCTAssertEqual(client.removalReasonRequests, ["order-1"])
        XCTAssertEqual(vm.removal?.reason, "The customer asked for another cleaner.")
    }

    func testTheReasonIsShownEvenWhenTheJobIsNoLongerVisibleToTheRemovedCleaner() async {
        refused()
        let vm = makeVM(showRemovalReason: true)

        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected the job to stay refused") }
        XCTAssertEqual(vm.removal?.reason, "The customer asked for another cleaner.")
        XCTAssertNil(snackbar.current, "the page answers the refusal; nothing is toasted over it")
    }

    /// A cover handed over reuses the notice with no removal row: the same page, saying the job left her schedule.
    func testARemovalTheServerCannotFindIsTheSamePageWithNoReasonAndRaisesNothing() async {
        refused()
        client.removalReasonResult = .failure(ApiError(code: "order.not_found", httpStatus: 404))
        let vm = makeVM(showRemovalReason: true)

        await vm.load()

        XCTAssertNotNil(vm.removal)
        XCTAssertNil(vm.removal?.reason)
        XCTAssertNil(snackbar.current)
    }

    func testAnOrdinaryOpenNeverAsksWhyTheCleanerWasRemoved() async {
        let vm = makeVM(showRemovalReason: false)

        await vm.load()

        XCTAssertTrue(client.removalReasonRequests.isEmpty)
        XCTAssertNil(vm.removal)
    }

    /// The detail re-loads on every appearance and after every note or photo change; the reason is asked once.
    func testTheReasonIsAskedForOnceAcrossReloads() async {
        refused()
        let vm = makeVM(showRemovalReason: true)
        await vm.load()

        await vm.load()

        XCTAssertEqual(client.removalReasonRequests.count, 1)
        XCTAssertEqual(vm.removal?.reason, "The customer asked for another cleaner.")
    }

    /// The board loaded before the removal still holds her row: the page names the job from it, and her next jobs
    /// leave it out even though the store still lists it.
    func testThePageNamesTheJobFromTheStoreAndLeavesItOutOfHerNextJobs() async {
        refused()
        let removed = row("A2", status: ._2)
        store.publish(own: [row("A1", status: ._2), removed, row("A3", status: ._2)])
        store.publish(available: [row("V1"), row("V3")])
        let vm = makeVM(showRemovalReason: true, orderId: "A2")

        await vm.load()

        XCTAssertEqual(vm.removal?.job?.id, "A2")
        XCTAssertEqual(vm.removal?.nextJobs.loadedValue?.compactMap(\.id), ["A1", "A3"])
        XCTAssertEqual(vm.removal?.openJobs.loadedValue?.compactMap(\.id), ["V1", "V3"])
        XCTAssertEqual(client.getPagedCallCount, 0, "the store's lists are not read again")
    }

    /// A cold start from the push: no row, so no job line; the page reads both lists itself, as the tabs do.
    func testAColdStartReadsBothListsItselfAndNamesNoJob() async {
        refused()
        client.pageResponder = { query in
            let rows = query.hasAvailableSpots == true ? [row("V1")] : [row("A1", status: ._2), row("A2", status: ._2)]
            return .success(OrderPage(rows: rows, total: rows.count))
        }
        let vm = makeVM(showRemovalReason: true, orderId: "A2")

        await vm.load()

        XCTAssertNil(vm.removal?.job)
        XCTAssertEqual(vm.removal?.nextJobs.loadedValue?.compactMap(\.id), ["A1"])
        XCTAssertEqual(vm.removal?.openJobs.loadedValue?.compactMap(\.id), ["V1"])
        let own = client.queries.first { $0.hasAvailableSpots != true }
        XCTAssertEqual(own?.statuses, [._2, ._3, ._4])
        XCTAssertEqual(own?.employeeId, "emp-self")
    }

    func testAListWhoseReadFailedIsLeftOutAndCheckAgainReadsBoth() async {
        refused()
        client.pageResponder = { query in
            query.hasAvailableSpots == true
                ? .failure(ApiError(code: "network.unreachable"))
                : .success(OrderPage(rows: [row("A1", status: ._2)], total: 1))
        }
        let vm = makeVM(showRemovalReason: true, orderId: "A2")
        await vm.load()
        guard case .error = vm.removal?.openJobs else { return XCTFail("the open jobs failed") }
        XCTAssertEqual(vm.removal?.nextJobs.loadedValue?.count, 1)

        client.pageResponder = { _ in .failure(ApiError(code: "network.unreachable")) }
        let reads = client.getPagedCallCount
        await vm.checkAgain()

        XCTAssertEqual(client.getPagedCallCount, reads + 2)
        guard case .error = vm.removal?.nextJobs, case .error = vm.removal?.openJobs else {
            return XCTFail("both reads failed")
        }
    }

    /// Offline the removal read is not an answer: the page is the ordinary failure with Retry, never "Job reassigned",
    /// and Retry asks for the reason again.
    func testARemovalReadThatFailedIsNoAnswerAndRetryAsksAgain() async {
        refused()
        client.removalReasonResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeVM(showRemovalReason: true)

        await vm.load()

        XCTAssertNil(vm.removal)
        XCTAssertFalse(vm.isGone, "the ordinary failure, which offers Retry")
        client.removalReasonResult = .success("The customer asked for another cleaner.")
        await vm.load()
        XCTAssertEqual(client.removalReasonRequests.count, 2)
        XCTAssertEqual(vm.removal?.reason, "The customer asked for another cleaner.")
    }

    /// Refused while the board still holds her row, with the removal read failed: nothing is drawn from that row, so
    /// a job she was taken off does not stay on screen as hers while the page waits for Retry.
    func testARefusedJobIsNotDrawnFromHerStoredRowWhileTheRemovalReadHasNotAnswered() async {
        store.publish(own: [row("order-1", status: ._2)])
        refused()
        client.removalReasonResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeVM(showRemovalReason: true)
        XCTAssertEqual(vm.rowDetail?.id, "order-1", "while the job loads, the board's row draws it")

        await vm.load()

        XCTAssertNil(vm.rowDetail)
        XCTAssertNil(vm.removal)
        XCTAssertFalse(vm.isGone, "the ordinary failure, which offers Retry")
        client.removalReasonResult = .success("The customer asked for another cleaner.")
        await vm.load()
        XCTAssertEqual(vm.removal?.reason, "The customer asked for another cleaner.")
    }

    /// A job that only failed to load is not one she was taken off, whatever the removal read said; once the server
    /// refuses it the page opens on the answer already read.
    func testAJobThatFailedToLoadIsNoRemovalUntilTheServerRefusesIt() async {
        client.byIdResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeVM(showRemovalReason: true)

        await vm.load()

        XCTAssertNil(vm.removal)
        refused()
        await vm.load()
        XCTAssertEqual(vm.removal?.reason, "The customer asked for another cleaner.")
        XCTAssertEqual(client.removalReasonRequests.count, 1)
    }

    /// *See N open jobs* is the Available badge: the store's total when it holds the list, else the page's own read's.
    func testTheOpenJobsCountIsTheServersTotal() async {
        refused()
        store.publish(available: [row("V1")], total: 12)
        let stored = makeVM(showRemovalReason: true, orderId: "A2")
        await stored.load()
        XCTAssertEqual(stored.removal?.openTotal, 12)
        XCTAssertEqual(stored.storedOpenTotal, 12)

        store = OrdersBoardStore()
        client.pageResponder = { query in
            query.hasAvailableSpots == true
                ? .success(OrderPage(rows: [row("V1")], total: 40))
                : .success(OrderPage(rows: [], total: 0))
        }
        let read = makeVM(showRemovalReason: true, orderId: "A2")
        await read.load()
        XCTAssertEqual(read.removal?.openTotal, 40)
    }

    /// The lead stops at its colon: the reason goes in its own well, so the key carries no slot.
    func testTheLeadIsTheMessageUpToItsColonWithNoSlot() {
        XCTAssertFalse(L10n.Orders.removalLead.contains("%"))
        XCTAssertTrue(L10n.Orders.removalLead.hasSuffix(":"))
    }
}

private func row(_ id: String, status: OrderStatus = ._0) -> OrderListItem {
    OrderListItem.sample(id: id, status: status, cleaningDateTime: Date(timeIntervalSince1970: 1_786_000_000))
}
