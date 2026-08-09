import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// `GetOrderPhotos` answers only a caller the strict `CanAccessOrderAsync` admits, and every photo is
/// an interior view of a private dwelling handed over as a forwardable one-hour signed URL. A cleaner
/// browsing an order they have not taken must therefore not fetch at all: firing the request and
/// swallowing the refusal would still be a request nobody was entitled to make, and firing it without
/// swallowing puts "Order not found" over a detail screen that is otherwise correct.
///
/// The gate is the same value that decides whether the rails are on screen, so the two cannot drift —
/// Android gets that for free (its `PhotosSection` creates the view model on entering composition, so
/// the fetch cannot outlive the section), and this is how iOS buys the same property.
@MainActor
final class OrderPhotosGateTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var staleness: OrdersStaleness!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        staleness = OrdersStaleness()
        snackbar = SnackbarController()
    }

    // MARK: the fetch

    func testABrowsingCleanerFetchesNoPhotosAndSeesNoToast() async {
        client.getPhotosResult = .failure(ApiError(code: "order.not_found", httpStatus: 400))
        let vm = makePhotosVM()

        await vm.load(isAuthorized: false)

        XCTAssertEqual(client.getPhotosCallCount, 0)
        XCTAssertNil(snackbar.current)
    }

    /// The rails render a spinner for `.loading`, which is the state the view model starts in. A gate
    /// that merely skips the call would leave that spinner turning forever the moment the section
    /// becomes visible, so the closed gate has to leave a settled state behind.
    func testABrowsingCleanerLandsOnASettledEmptyListRatherThanASpinner() async {
        let vm = makePhotosVM()

        await vm.load(isAuthorized: false)

        XCTAssertFalse(vm.state.isLoading)
        XCTAssertEqual(vm.state.loadedValue?.count, 0)
    }

    func testTheAssigneeStillFetchesAndRenders() async {
        client.getPhotosResult = .success([OrderPhoto(id: "p1", photoType: ._1, blobUrl: nil)])
        let vm = makePhotosVM()

        await vm.load(isAuthorized: true)

        XCTAssertEqual(client.getPhotosCallCount, 1)
        XCTAssertEqual(vm.state.loadedValue?.map(\.id), ["p1"])
    }

    /// Over-gating would be just as wrong: a real failure for the assignee must still reach them.
    func testAFailureForTheAssigneeStillSurfaces() async {
        client.getPhotosResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makePhotosVM()

        await vm.load(isAuthorized: true)

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    /// The post-mutation refetch is reached only from the rails, which only exist for the assignee,
    /// so it must not have to re-ask — and must not be suppressed by the gate's default.
    func testAPhotoMutationStillRefetches() async {
        client.getPhotosResult = .success([])
        let vm = makePhotosVM()
        await vm.load(isAuthorized: true)
        let before = client.getPhotosCallCount

        await vm.delete(photoId: "p1")

        XCTAssertEqual(client.getPhotosCallCount, before + 1)
    }

    // MARK: the gate

    func testTheGateIsClosedWhileTheOrderIsStillLoading() {
        XCTAssertFalse(makeDetailVM().canReadPhotos)
    }

    func testTheGateIsClosedForAnOrderTheCleanerHasNotTaken() async {
        client.byIdResult = .success(item(status: 2, isMine: false))
        let vm = makeDetailVM()

        await vm.load()

        XCTAssertFalse(vm.canReadPhotos)
    }

    func testTheGateIsClosedWhenTheOrderItselfFailedToLoad() async {
        client.byIdResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeDetailVM()

        await vm.load()

        XCTAssertFalse(vm.canReadPhotos)
    }

    func testTheGateIsOpenForTheAssigneeOnALiveOrder() async {
        client.byIdResult = .success(item(status: 4, isMine: true))
        let vm = makeDetailVM()

        await vm.load()

        XCTAssertTrue(vm.canReadPhotos)
    }

    /// The state the one-shot fix misses: the cleaner takes the job with the detail already open, the
    /// rails appear, and a gate decided once at screen-open would leave them stuck on their spinner.
    func testTheGateOpensWhenTheCleanerTakesTheOrderWithTheDetailOpen() async {
        client.byIdResult = .success(item(status: 0, isMine: false))
        let vm = makeDetailVM()
        await vm.load()
        XCTAssertFalse(vm.canReadPhotos)

        client.byIdResult = .success(item(status: 2, isMine: true))
        await vm.take()

        XCTAssertTrue(vm.canReadPhotos)
    }

    /// Parity: Android never composes the section on a finished job, so it never fetches there either.
    func testTheGateClosesAgainOnceTheOrderIsTerminal() async {
        client.byIdResult = .success(item(status: 4, isMine: true))
        let vm = makeDetailVM()
        await vm.load()
        XCTAssertTrue(vm.canReadPhotos)

        client.byIdResult = .success(item(status: 5, isMine: true))
        await vm.load()

        XCTAssertFalse(vm.canReadPhotos)
    }

    // MARK: the one predicate both the rails and the fetch read

    func testWorkSectionsNeedBothTheAssignmentAndALiveStatus() {
        for status in [0, 2, 3, 4, 5, 6] {
            XCTAssertFalse(
                OrderDetail(item(status: status, isMine: false)).showsWorkSections,
                "status \(status) must stay closed for a cleaner who has not taken the order"
            )
        }
        for status in [2, 3, 4] {
            XCTAssertTrue(
                OrderDetail(item(status: status, isMine: true)).showsWorkSections,
                "status \(status) is live work for the assignee"
            )
        }
        for status in [0, 5, 6] {
            XCTAssertFalse(
                OrderDetail(item(status: status, isMine: true)).showsWorkSections,
                "status \(status) is not live work"
            )
        }
    }

    private func makePhotosVM() -> OrderPhotosViewModel {
        OrderPhotosViewModel(orderId: "order-1", client: client, snackbar: snackbar)
    }

    private func makeDetailVM() -> OrderDetailViewModel {
        OrderDetailViewModel(orderId: "order-1", client: client, staleness: staleness, snackbar: snackbar)
    }

    private func item(status: Int, isMine: Bool) -> OrderItem {
        var item = OrderItem()
        item.id = "order-1"
        item.displayOrderNumber = "ORD-1"
        item.orderStatus = Code(value: status)
        item.address = OrderAddress(latitude: 50.0, longitude: 14.0)
        item.isAssignedToCurrentUser = isMine
        item.hasAfterPhotos = false
        return item
    }
}
