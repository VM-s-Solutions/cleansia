import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// A cleaner taken off a job by an administrator is told why: the removal notice opens the detail, and
/// only that entry asks the server for the written reason.
@MainActor
final class OrderDetailRemovalReasonTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var staleness: OrdersStaleness!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        staleness = OrdersStaleness()
        snackbar = SnackbarController()
    }

    private func makeVM(showRemovalReason: Bool) -> OrderDetailViewModel {
        OrderDetailViewModel(
            orderId: "order-1",
            client: client,
            staleness: staleness,
            snackbar: snackbar,
            pendingOffers: PendingOffersStore(client: client, ordersStaleness: staleness),
            showRemovalReason: showRemovalReason
        )
    }

    func testOpenedFromTheRemovalNoticeTheAdministratorsReasonIsFetchedAndShown() async {
        client.removalReasonResult = .success("The customer asked for another cleaner.")
        let vm = makeVM(showRemovalReason: true)

        await vm.load()

        XCTAssertEqual(client.removalReasonRequests, ["order-1"])
        XCTAssertEqual(vm.removalReason, "The customer asked for another cleaner.")
    }

    func testTheReasonIsShownEvenWhenTheJobIsNoLongerVisibleToTheRemovedCleaner() async {
        client.byIdResult = .failure(ApiError(code: "order.not_found", httpStatus: 404))
        let vm = makeVM(showRemovalReason: true)

        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected the error state") }
        XCTAssertEqual(vm.removalReason, "The customer asked for another cleaner.")
    }

    func testDismissingTheReasonClosesIt() async {
        let vm = makeVM(showRemovalReason: true)
        await vm.load()

        vm.dismissRemovalReason()

        XCTAssertNil(vm.removalReason)
    }

    func testARemovalTheServerCannotFindShowsNothingAndRaisesNothing() async {
        client.removalReasonResult = .failure(ApiError(code: "order.not_found", httpStatus: 404))
        let vm = makeVM(showRemovalReason: true)

        await vm.load()

        XCTAssertNil(vm.removalReason)
        XCTAssertNil(snackbar.current)
    }

    func testAnOrdinaryOpenNeverAsksWhyTheCleanerWasRemoved() async {
        let vm = makeVM(showRemovalReason: false)

        await vm.load()

        XCTAssertTrue(client.removalReasonRequests.isEmpty)
        XCTAssertNil(vm.removalReason)
    }

    /// The detail re-loads on every appearance and after every note or photo change; a reason already
    /// dismissed must not come back.
    func testTheReasonIsAskedForOnceAcrossReloads() async {
        let vm = makeVM(showRemovalReason: true)
        await vm.load()
        vm.dismissRemovalReason()

        await vm.load()

        XCTAssertEqual(client.removalReasonRequests.count, 1)
        XCTAssertNil(vm.removalReason)
    }
}
