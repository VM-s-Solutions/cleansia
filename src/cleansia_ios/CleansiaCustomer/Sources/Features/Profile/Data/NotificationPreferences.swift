import CleansiaCore
import CleansiaCustomerApi
import Foundation

enum NotificationCategory: CaseIterable {
    case orderUpdates
    case cleanerOnTheWay
    case orderCompleted
    case orderCancelled
    case refundIssued
    case membershipExpiring
    case membershipCancelled
    case tierUpgrade
    case promo
    case disputeReply
    case recurringScheduled
}

struct NotificationPreferences: Equatable {
    var orderUpdates: Bool
    var cleanerOnTheWay: Bool
    var orderCompleted: Bool
    var orderCancelled: Bool
    var refundIssued: Bool
    var membershipExpiring: Bool
    var membershipCancelled: Bool
    var tierUpgrade: Bool
    var promo: Bool
    var disputeReply: Bool
    var recurringScheduled: Bool

    static let keyPaths: [NotificationCategory: WritableKeyPath<NotificationPreferences, Bool>] = [
        .orderUpdates: \.orderUpdates,
        .cleanerOnTheWay: \.cleanerOnTheWay,
        .orderCompleted: \.orderCompleted,
        .orderCancelled: \.orderCancelled,
        .refundIssued: \.refundIssued,
        .membershipExpiring: \.membershipExpiring,
        .membershipCancelled: \.membershipCancelled,
        .tierUpgrade: \.tierUpgrade,
        .promo: \.promo,
        .disputeReply: \.disputeReply,
        .recurringScheduled: \.recurringScheduled
    ]

    func isEnabled(_ category: NotificationCategory) -> Bool {
        guard let keyPath = Self.keyPaths[category] else { return false }
        return self[keyPath: keyPath]
    }

    func with(_ category: NotificationCategory, enabled: Bool) -> NotificationPreferences {
        guard let keyPath = Self.keyPaths[category] else { return self }
        var copy = self
        copy[keyPath: keyPath] = enabled
        return copy
    }
}

protocol NotificationPreferencesClient: AnyObject {
    func getMine() async -> ApiResult<NotificationPreferences>
    func update(_ preferences: NotificationPreferences) async -> ApiResult<NotificationPreferences>
}

final class LiveNotificationPreferencesClient: NotificationPreferencesClient {
    func getMine() async -> ApiResult<NotificationPreferences> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerNotificationPreferencesAPI.notificationPreferencesGetMine().toDomain()
        }
    }

    func update(_ preferences: NotificationPreferences) async -> ApiResult<NotificationPreferences> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerNotificationPreferencesAPI
                .notificationPreferencesUpdate(updateNotificationPreferencesCommand: preferences.toCommand())
                .toDomain()
        }
    }
}

private extension NotificationPreferencesDto {
    func toDomain() -> NotificationPreferences {
        NotificationPreferences(
            orderUpdates: orderUpdates ?? true,
            cleanerOnTheWay: cleanerOnTheWay ?? true,
            orderCompleted: orderCompleted ?? true,
            orderCancelled: orderCancelled ?? true,
            refundIssued: refundIssued ?? true,
            membershipExpiring: membershipExpiring ?? true,
            membershipCancelled: membershipCancelled ?? true,
            tierUpgrade: tierUpgrade ?? true,
            promo: promo ?? false,
            disputeReply: disputeReply ?? true,
            recurringScheduled: recurringScheduled ?? true
        )
    }
}

private extension NotificationPreferences {
    func toCommand() -> UpdateNotificationPreferencesCommand {
        UpdateNotificationPreferencesCommand(
            orderUpdates: orderUpdates,
            cleanerOnTheWay: cleanerOnTheWay,
            orderCompleted: orderCompleted,
            orderCancelled: orderCancelled,
            refundIssued: refundIssued,
            membershipExpiring: membershipExpiring,
            membershipCancelled: membershipCancelled,
            tierUpgrade: tierUpgrade,
            promo: promo,
            disputeReply: disputeReply,
            recurringScheduled: recurringScheduled
        )
    }
}
