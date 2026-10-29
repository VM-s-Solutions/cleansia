import CleansiaCore
import CleansiaCustomerApi
import Foundation
import XCTest
@testable import CleansiaCustomer

private final class RecoveryActivityRecorder: OrderLiveActivitySyncing, @unchecked Sendable {
    var starts: [String] = []
    var ends: [String] = []
    var resumes = 0
    var tracked: Set<String> = []

    func start(orderId: String, orderNumber _: String, status _: String, window _: EtaWindow) {
        starts.append(orderId)
    }

    func update(orderId _: String, orderNumber _: String, status _: String, window _: EtaWindow) {}
    func end(orderId: String, orderNumber _: String, status _: LiveActivityTerminalStatus) {
        ends.append(orderId)
    }

    func resumeRegistrations() {
        resumes += 1
    }

    func tracks(orderId: String, orderNumber _: String) -> Bool {
        tracked.contains(orderId)
    }
}

@MainActor
final class LiveActivityForegroundRecoveryTests: XCTestCase {
    private func drain() async {
        for _ in 0 ..< 30 {
            await Task.yield()
        }
    }

    private func detail(_ status: Int = 4) -> CustomerOrderDetail {
        OrderFixtures.detail(
            id: "active",
            statusCode: Code(type: "OrderStatus", name: nil, value: status),
            displayOrderNumber: "1042",
            cleaningDateTime: Date(),
            estimatedMinutes: 60
        )
    }

    func testExistingOrderRefreshRecoversOnlyEligibleNonGuestOrdersWithoutOpeningDetail() async {
        let client = FakeOrderClient()
        var guest = OrderFixtures.summary(id: "guest", statusValue: 4)
        guest.isGuestBooking = true
        client.pages = [OrdersPage(items: [
            OrderFixtures.summary(id: "active", statusValue: 4),
            OrderFixtures.summary(id: "future", statusValue: 2),
            guest
        ], total: 3)]
        client.detailResults = [.success(detail())]
        let repository = OrderRepository(client: client)
        let activity = RecoveryActivityRecorder()
        let recovery = CustomerLiveActivityRecovery(client: client, repository: repository, liveActivity: activity)
        recovery.setSessionActive(true)

        _ = await repository.refresh()
        await drain()

        XCTAssertEqual(activity.starts, ["active"])
        XCTAssertEqual(client.detailCallCount, 1)
        XCTAssertEqual(client.pageRequests.count, 1, "recovery must use the existing page, not fetch a second list")
    }

    func testTerminalSummaryEndsCardWithoutAnotherDetailRead() async {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [OrderFixtures.summary(id: "active", statusValue: 5)], total: 1)]
        let repository = OrderRepository(client: client)
        let activity = RecoveryActivityRecorder()
        activity.tracked = ["active"]
        let recovery = CustomerLiveActivityRecovery(client: client, repository: repository, liveActivity: activity)
        recovery.setSessionActive(true)
        _ = await repository.refresh()
        await drain()
        XCTAssertEqual(activity.ends, ["active"])
        XCTAssertEqual(client.detailCallCount, 0)
    }

    func testLogoutRejectsLateEligibleDetailAndForegroundDoesNotRetryWithoutSession() async {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [OrderFixtures.summary(id: "active", statusValue: 4)], total: 1)]
        client.holdDetails = true
        let repository = OrderRepository(client: client)
        let activity = RecoveryActivityRecorder()
        let recovery = CustomerLiveActivityRecovery(client: client, repository: repository, liveActivity: activity)
        recovery.setSessionActive(true)
        _ = await repository.refresh()
        await drain()
        await recovery.clear()
        client.releaseDetail(.success(detail()))
        await drain()
        recovery.resume()
        await drain()
        XCTAssertTrue(activity.starts.isEmpty)
        XCTAssertEqual(activity.resumes, 0)
    }

    func testActiveOrderInLoadedLaterPageIsEligibleBeforeDetailBudgetIsApplied() async {
        let client = FakeOrderClient()
        let firstPage = (0 ..< 20).map { OrderFixtures.summary(id: "future-\($0)", statusValue: 2) }
        client.pages = [
            OrdersPage(items: firstPage, total: 21),
            OrdersPage(items: [OrderFixtures.summary(id: "active", statusValue: 4)], total: 21)
        ]
        client.detailResults = [.success(detail())]
        let repository = OrderRepository(client: client)
        let activity = RecoveryActivityRecorder()
        let recovery = CustomerLiveActivityRecovery(client: client, repository: repository, liveActivity: activity)
        recovery.setSessionActive(true)
        _ = await repository.refresh()
        await drain()
        _ = await repository.loadNextPage()
        await drain()
        XCTAssertEqual(activity.starts, ["active"])
        XCTAssertEqual(client.detailCallCount, 1)
        XCTAssertEqual(client.pageRequests.count, 2)
    }

    func testSuccessfulPublicationThenResumeDoesNotFetchTheSameDetailsTwice() async {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [OrderFixtures.summary(id: "active", statusValue: 4)], total: 1)]
        client.detailResults = [.success(detail())]
        let repository = OrderRepository(client: client)
        let activity = RecoveryActivityRecorder()
        let recovery = CustomerLiveActivityRecovery(client: client, repository: repository, liveActivity: activity)
        recovery.setSessionActive(true)
        let result = await repository.refresh()
        await drain()
        recovery.resume(afterRefresh: result)
        await drain()
        XCTAssertEqual(client.detailCallCount, 1)
        XCTAssertEqual(activity.resumes, 1)
    }

    func testFailedForegroundRefreshRetriesCachedEligibleOrderAfterFailedDetail() async {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [OrderFixtures.summary(id: "active", statusValue: 4)], total: 1)]
        client.detailResults = [.failure(ApiError(httpStatus: 503)), .success(detail())]
        let repository = OrderRepository(client: client)
        let activity = RecoveryActivityRecorder()
        let recovery = CustomerLiveActivityRecovery(client: client, repository: repository, liveActivity: activity)
        recovery.setSessionActive(true)
        _ = await repository.refresh()
        await drain()
        XCTAssertTrue(activity.starts.isEmpty)
        client.pageError = ApiError(httpStatus: 503)
        await recovery.refreshForForeground()
        await drain()
        XCTAssertEqual(client.detailCallCount, 2)
        XCTAssertEqual(activity.starts, ["active"])
    }

    func testIdenticalSnapshotResumeCoalescesAnInflightDetailRead() async {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [OrderFixtures.summary(id: "active", statusValue: 4)], total: 1)]
        client.holdDetails = true
        let repository = OrderRepository(client: client)
        let activity = RecoveryActivityRecorder()
        let recovery = CustomerLiveActivityRecovery(client: client, repository: repository, liveActivity: activity)
        recovery.setSessionActive(true)
        _ = await repository.refresh()
        await drain()
        recovery.resume()
        await drain()
        client.releaseDetail(.success(detail()))
        await drain()
        XCTAssertEqual(client.detailCallCount, 1)
        XCTAssertEqual(activity.starts, ["active"])
    }
}
