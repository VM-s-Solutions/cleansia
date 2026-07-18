import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct UserNotification: Equatable, Identifiable {
    let id: String
    let eventKey: String
    let args: [String: String]
    let createdOn: Date
    let readOn: Date?

    func markedRead(on date: Date) -> UserNotification {
        UserNotification(id: id, eventKey: eventKey, args: args, createdOn: createdOn, readOn: readOn ?? date)
    }
}

struct UserNotificationPage: Equatable {
    let items: [UserNotification]
    let total: Int
}

/// The `audience` wire field is enriched server-side from the calling host;
/// the client never sends or reads it.
protocol NotificationFeedClient: Sendable {
    func page(offset: Int, limit: Int) async -> ApiResult<UserNotificationPage>
    func unreadCount() async -> ApiResult<Int>
    func markRead(id: String) async -> ApiResult<Void>
    func markAllRead(upToCreatedOn: Date) async -> ApiResult<Void>
}

struct LiveNotificationFeedClient: NotificationFeedClient {
    func page(offset: Int, limit: Int) async -> ApiResult<UserNotificationPage> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerNotificationAPI.notificationGetPaged(offset: offset, limit: limit)
        }
        return result.map { paged in
            UserNotificationPage(
                items: (paged.data ?? []).compactMap(UserNotification.init),
                total: paged.total ?? 0
            )
        }
    }

    func unreadCount() async -> ApiResult<Int> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerNotificationAPI.notificationUnreadCount()
        }
        return result.map { $0.count ?? 0 }
    }

    func markRead(id: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await CustomerNotificationAPI.notificationMarkRead(
                markNotificationReadCommand: MarkNotificationReadCommand(id: id)
            )
        }
    }

    func markAllRead(upToCreatedOn: Date) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await CustomerNotificationAPI.notificationMarkAllRead(
                markAllNotificationsReadCommand: MarkAllNotificationsReadCommand(upToCreatedOn: upToCreatedOn)
            )
        }
    }
}

extension UserNotification {
    init?(_ dto: UserNotificationDto) {
        guard let id = dto.id, !id.isEmpty,
              let eventKey = dto.eventKey, !eventKey.isEmpty,
              let createdOn = dto.createdOn
        else { return nil }
        self.init(id: id, eventKey: eventKey, args: dto.args ?? [:], createdOn: createdOn, readOn: dto.readOn)
    }
}
