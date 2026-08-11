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

/// **Refuse.** All eleven are non-nullable `bool` on the server, and the screen writes back every
/// one of them: `toCommand()` sends the whole struct, so a defaulted toggle is not a display guess —
/// it is the value that overwrites the customer's stored choice the next time they save anything on
/// this screen. Defaulting to `true` re-subscribes someone who opted out; defaulting `promo` to
/// `false` discards a marketing consent they gave. An error screen loses nothing by comparison.
extension NotificationPreferencesDto {
    func toDomain() throws -> NotificationPreferences {
        try NotificationPreferences(
            orderUpdates: orderUpdates.require("orderUpdates"),
            cleanerOnTheWay: cleanerOnTheWay.require("cleanerOnTheWay"),
            orderCompleted: orderCompleted.require("orderCompleted"),
            orderCancelled: orderCancelled.require("orderCancelled"),
            refundIssued: refundIssued.require("refundIssued"),
            membershipExpiring: membershipExpiring.require("membershipExpiring"),
            membershipCancelled: membershipCancelled.require("membershipCancelled"),
            tierUpgrade: tierUpgrade.require("tierUpgrade"),
            promo: promo.require("promo"),
            disputeReply: disputeReply.require("disputeReply"),
            recurringScheduled: recurringScheduled.require("recurringScheduled")
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
