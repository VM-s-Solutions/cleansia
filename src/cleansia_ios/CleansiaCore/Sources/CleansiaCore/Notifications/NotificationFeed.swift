import Foundation

public struct UserNotification: Equatable, Identifiable {
    public let id: String
    public let eventKey: String
    public let args: [String: String]
    public let createdOn: Date
    public let readOn: Date?

    public init(id: String, eventKey: String, args: [String: String], createdOn: Date, readOn: Date?) {
        self.id = id
        self.eventKey = eventKey
        self.args = args
        self.createdOn = createdOn
        self.readOn = readOn
    }

    public func markedRead(on date: Date) -> UserNotification {
        UserNotification(id: id, eventKey: eventKey, args: args, createdOn: createdOn, readOn: readOn ?? date)
    }
}

public struct UserNotificationPage: Equatable {
    public let items: [UserNotification]
    public let total: Int

    public init(items: [UserNotification], total: Int) {
        self.items = items
        self.total = total
    }
}

/// The `audience` wire field is enriched server-side from the calling host;
/// the client never sends or reads it.
public protocol NotificationFeedClient: Sendable {
    func page(offset: Int, limit: Int) async -> ApiResult<UserNotificationPage>
    func unreadCount() async -> ApiResult<Int>
    func markRead(id: String) async -> ApiResult<Void>
    func markAllRead(upToCreatedOn: Date) async -> ApiResult<Void>
}

/// A feed row as the inbox draws it, rendered by each app from its own push templates.
public struct NotificationFeedRow: Equatable, Identifiable {
    public let id: String
    public let title: String
    public let body: String
    public let createdOn: Date
    public let isUnread: Bool

    public init(id: String, title: String, body: String, createdOn: Date, isUnread: Bool) {
        self.id = id
        self.title = title
        self.body = body
        self.createdOn = createdOn
        self.isUnread = isUnread
    }
}
