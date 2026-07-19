import CleansiaCore
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class NotificationsInboxViewModelTests: XCTestCase {
    private var client: FakeNotificationFeedClient!
    private var badge: NotificationBadgeModel!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakeNotificationFeedClient()
        badge = NotificationBadgeModel(client: client)
        snackbar = SnackbarController()
    }

    private func makeVM(pageSize: Int = 20) -> NotificationsInboxViewModel {
        NotificationsInboxViewModel(client: client, badge: badge, snackbar: snackbar, pageSize: pageSize)
    }

    func testInitialStateIsLoading() {
        XCTAssertTrue(makeVM().state.isLoading)
    }

    func testOpenLoadsPageOneWithPageSize20() async {
        client.pageResults = [NotificationFixtures.page([NotificationFixtures.item()])]
        let vm = makeVM()
        await vm.onOpen()
        XCTAssertEqual(client.pageRequests.count, 1)
        XCTAssertEqual(client.pageRequests.first?.offset, 0)
        XCTAssertEqual(client.pageRequests.first?.limit, 20)
        XCTAssertEqual(vm.state.loadedValue?.count, 1)
    }

    func testOpenMarksAllReadWatermarkedAtTheNewestFetchedRow() async {
        let older = NotificationFixtures.item(id: "n-1", createdOn: Date(timeIntervalSince1970: 100))
        let newest = NotificationFixtures.item(id: "n-2", createdOn: Date(timeIntervalSince1970: 900))
        let mid = NotificationFixtures.item(id: "n-3", createdOn: Date(timeIntervalSince1970: 500))
        client.pageResults = [NotificationFixtures.page([newest, mid, older])]
        let vm = makeVM()
        await vm.onOpen()
        XCTAssertEqual(client.markAllWatermarks, [Date(timeIntervalSince1970: 900)])
    }

    func testOpenWithZeroRowsDoesNotMarkAllRead() async {
        client.pageResults = [NotificationFixtures.page([])]
        let vm = makeVM()
        await vm.onOpen()
        XCTAssertEqual(vm.state.loadedValue, [])
        XCTAssertTrue(client.markAllWatermarks.isEmpty)
    }

    func testOpenRefreshesTheBadgeAfterMarkAll() async {
        badge.notePushReceived(eventKey: "order.new_available")
        client.pageResults = [NotificationFixtures.page([NotificationFixtures.item()])]
        client.unreadCountResult = .success(0)
        let vm = makeVM()
        await vm.onOpen()
        XCTAssertEqual(client.unreadCountCallCount, 1)
        XCTAssertEqual(badge.unreadCount, 0)
    }

    func testMarkAllFailureSkipsTheBadgeRefresh() async {
        client.pageResults = [NotificationFixtures.page([NotificationFixtures.item()])]
        client.markAllReadResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.onOpen()
        XCTAssertEqual(client.unreadCountCallCount, 0)
    }

    func testOpenTwiceFetchesOnce() async {
        client.pageResults = [NotificationFixtures.page([NotificationFixtures.item()])]
        let vm = makeVM()
        await vm.onOpen()
        await vm.onOpen()
        XCTAssertEqual(client.pageRequests.count, 1)
    }

    func testOpenFailureTransitionsToErrorAndSnackbars() async {
        client.pageResults = [.failure(ApiError(httpStatus: 500))]
        let vm = makeVM()
        await vm.onOpen()
        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
        XCTAssertTrue(client.markAllWatermarks.isEmpty)
    }

    func testRetryAfterFailureLoads() async {
        client.pageResults = [
            .failure(ApiError(httpStatus: 500)),
            NotificationFixtures.page([NotificationFixtures.item()])
        ]
        let vm = makeVM()
        await vm.onOpen()
        await vm.retry()
        XCTAssertEqual(vm.state.loadedValue?.count, 1)
    }

    func testLoadNextPageAppends() async {
        let first = (0 ..< 2).map { NotificationFixtures.item(id: "n-\($0)") }
        let second = [NotificationFixtures.item(id: "n-9")]
        client.pageResults = [
            NotificationFixtures.page(first, total: 3),
            NotificationFixtures.page(second, total: 3)
        ]
        let vm = makeVM(pageSize: 2)
        await vm.onOpen()
        XCTAssertTrue(vm.hasMore)
        await vm.loadNextPage()
        XCTAssertEqual(vm.state.loadedValue?.count, 3)
        XCTAssertFalse(vm.hasMore)
        XCTAssertEqual(client.pageRequests.last?.offset, 2)
    }

    func testLoadNextPageKeepsTheFirstPageWatermark() async {
        let first = [NotificationFixtures.item(id: "n-1", createdOn: Date(timeIntervalSince1970: 900))]
        let second = [NotificationFixtures.item(id: "n-2", createdOn: Date(timeIntervalSince1970: 100))]
        client.pageResults = [
            NotificationFixtures.page(first, total: 2),
            NotificationFixtures.page(second, total: 2)
        ]
        let vm = makeVM(pageSize: 1)
        await vm.onOpen()
        await vm.loadNextPage()
        XCTAssertEqual(client.markAllWatermarks, [Date(timeIntervalSince1970: 900)])
    }

    func testTapUnreadRowSendsMarkReadOnceAndFlipsOptimistically() async {
        badge.notePushReceived(eventKey: "order.new_available")
        client.pageResults = [NotificationFixtures.page([NotificationFixtures.item(id: "n-1")])]
        client.markAllReadResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.onOpen()

        await vm.tap(id: "n-1")

        XCTAssertEqual(client.markReadIds, ["n-1"])
        XCTAssertNotNil(vm.state.loadedValue?.first?.readOn)
        XCTAssertEqual(badge.unreadCount, 0)
    }

    func testTapReadRowSkipsMarkReadAndTheBadge() async {
        badge.notePushReceived(eventKey: "order.new_available")
        client.pageResults = [NotificationFixtures.page([
            NotificationFixtures.item(id: "n-1", readOn: Date())
        ])]
        client.markAllReadResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.onOpen()

        await vm.tap(id: "n-1")

        XCTAssertEqual(badge.unreadCount, 1)
        XCTAssertTrue(client.markReadIds.isEmpty)
    }

    func testTapNewAvailableRowEmitsTheOrdersTabDestination() async {
        client.pageResults = [NotificationFixtures.page([
            NotificationFixtures.item(id: "n-1", eventKey: "order.new_available", args: ["count": "2"])
        ])]
        let vm = makeVM()
        await vm.onOpen()

        var received: [PartnerNotificationDestination] = []
        let token = vm.tapped.sink { received.append($0) }
        defer { token.cancel() }

        await vm.tap(id: "n-1")

        XCTAssertEqual(received, [.ordersTab])
    }

    func testTapRowWithoutTargetMarksReadWithoutNavigating() async {
        client.pageResults = [NotificationFixtures.page([
            NotificationFixtures.item(id: "n-1", eventKey: "order.confirmed", args: [:])
        ])]
        let vm = makeVM()
        await vm.onOpen()

        var received: [PartnerNotificationDestination] = []
        let token = vm.tapped.sink { received.append($0) }
        defer { token.cancel() }

        await vm.tap(id: "n-1")

        XCTAssertTrue(received.isEmpty)
        XCTAssertEqual(client.markReadIds, ["n-1"])
    }
}
