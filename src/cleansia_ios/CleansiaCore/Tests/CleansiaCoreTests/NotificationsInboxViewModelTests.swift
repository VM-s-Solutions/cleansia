import Combine
import XCTest
@testable import CleansiaCore

@MainActor
final class NotificationsInboxViewModelTests: XCTestCase {
    private var client: FakeNotificationFeedClient!
    private var badge: NotificationBadgeModel!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakeNotificationFeedClient()
        badge = NotificationBadgeModel(client: client, feedEventKeys: NotificationFixtures.feedEventKeys)
        snackbar = SnackbarController()
    }

    /// The destination is whatever the row names under `target`; a row naming none has none.
    private func makeVM(pageSize: Int = 20) -> NotificationsInboxViewModel<String> {
        NotificationsInboxViewModel(
            client: client,
            badge: badge,
            snackbar: snackbar,
            destination: { $0.args["target"] },
            pageSize: pageSize
        )
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
        badge.notePushReceived(eventKey: NotificationFixtures.feedEventKey)
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
        badge.notePushReceived(eventKey: NotificationFixtures.feedEventKey)
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
        badge.notePushReceived(eventKey: NotificationFixtures.feedEventKey)
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

    func testTapResolvableRowEmitsItsDestination() async {
        client.pageResults = [NotificationFixtures.page([
            NotificationFixtures.item(id: "n-1", args: ["target": "order-42"])
        ])]
        let vm = makeVM()
        await vm.onOpen()

        var received: [String] = []
        let token = vm.tapped.sink { received.append($0) }
        defer { token.cancel() }

        await vm.tap(id: "n-1")

        XCTAssertEqual(received, ["order-42"])
    }

    func testTapRowWithoutTargetMarksReadWithoutNavigating() async {
        client.pageResults = [NotificationFixtures.page([NotificationFixtures.item(id: "n-1", args: [:])])]
        let vm = makeVM()
        await vm.onOpen()

        var received: [String] = []
        let token = vm.tapped.sink { received.append($0) }
        defer { token.cancel() }

        await vm.tap(id: "n-1")

        XCTAssertTrue(received.isEmpty)
        XCTAssertEqual(client.markReadIds, ["n-1"])
    }
}

final class NotificationFeedFormatTests: XCTestCase {
    func testTimestampIsRelativeWithinAWeekAndTheDayAndTimeBeyond() {
        let now = Date(timeIntervalSince1970: 1_750_000_000)
        let locale = Locale(identifier: "cs")
        let dayAndTime = DateFormatter()
        dayAndTime.locale = locale
        dayAndTime.setLocalizedDateFormatFromTemplate("EEE d MMM HH:mm")

        let recent = NotificationFeedFormat.timestamp(now.addingTimeInterval(-3600), relativeTo: now, locale: locale)
        XCTAssertEqual(recent, "před 1 hodinou")

        let old = now.addingTimeInterval(-30 * 24 * 3600)
        XCTAssertEqual(
            NotificationFeedFormat.timestamp(old, relativeTo: now, locale: locale),
            dayAndTime.string(from: old)
        )
    }
}

final class FakeNotificationFeedClient: NotificationFeedClient, @unchecked Sendable {
    var pageResults: [ApiResult<UserNotificationPage>] = []
    var unreadCountResult: ApiResult<Int> = .success(0)
    var markReadResult: ApiResult<Void> = .success(())
    var markAllReadResult: ApiResult<Void> = .success(())

    private(set) var pageRequests: [(offset: Int, limit: Int)] = []
    private(set) var unreadCountCallCount = 0
    private(set) var markReadIds: [String] = []
    private(set) var markAllWatermarks: [Date] = []

    func page(offset: Int, limit: Int) async -> ApiResult<UserNotificationPage> {
        pageRequests.append((offset, limit))
        guard !pageResults.isEmpty else {
            return .success(UserNotificationPage(items: [], total: 0))
        }
        return pageResults.removeFirst()
    }

    var holdUnreadCount = false
    var unreadCountStarted: ((Int) -> Void)?
    private var heldUnreadCount: [Int: CheckedContinuation<ApiResult<Int>, Never>] = [:]

    @MainActor
    func unreadCount() async -> ApiResult<Int> {
        unreadCountCallCount += 1
        let call = unreadCountCallCount
        guard holdUnreadCount else {
            unreadCountStarted?(call)
            return unreadCountResult
        }
        return await withCheckedContinuation { continuation in
            heldUnreadCount[call] = continuation
            unreadCountStarted?(call)
        }
    }

    @MainActor
    func releaseUnreadCount(call: Int, result: ApiResult<Int>) {
        heldUnreadCount.removeValue(forKey: call)?.resume(returning: result)
    }

    func markRead(id: String) async -> ApiResult<Void> {
        markReadIds.append(id)
        return markReadResult
    }

    func markAllRead(upToCreatedOn: Date) async -> ApiResult<Void> {
        markAllWatermarks.append(upToCreatedOn)
        return markAllReadResult
    }
}

enum NotificationFixtures {
    static let feedEventKey = "order.feed_event"
    static let feedEventKeys: Set<String> = [feedEventKey]

    static func item(
        id: String = "n-1",
        eventKey: String = feedEventKey,
        args: [String: String] = ["count": "3"],
        createdOn: Date = Date(timeIntervalSince1970: 1_000_000),
        readOn: Date? = nil
    ) -> UserNotification {
        UserNotification(id: id, eventKey: eventKey, args: args, createdOn: createdOn, readOn: readOn)
    }

    static func page(_ items: [UserNotification], total: Int? = nil) -> ApiResult<UserNotificationPage> {
        .success(UserNotificationPage(items: items, total: total ?? items.count))
    }
}
