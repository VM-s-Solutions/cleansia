import CleansiaCore
import Foundation
@testable import CleansiaCustomer

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

    func unreadCount() async -> ApiResult<Int> {
        unreadCountCallCount += 1
        return unreadCountResult
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
    static func item(
        id: String = "n-1",
        eventKey: String = "order.confirmed",
        args: [String: String] = ["orderId": "o-1", "orderNumber": "A-1042"],
        createdOn: Date = Date(timeIntervalSince1970: 1_000_000),
        readOn: Date? = nil
    ) -> UserNotification {
        UserNotification(id: id, eventKey: eventKey, args: args, createdOn: createdOn, readOn: readOn)
    }

    static func page(_ items: [UserNotification], total: Int? = nil) -> ApiResult<UserNotificationPage> {
        .success(UserNotificationPage(items: items, total: total ?? items.count))
    }
}
