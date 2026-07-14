import Foundation

enum CustomerNotificationDestination: Equatable {
    case order(orderId: String)
    case dispute(disputeId: String)
    case subscribePlus
    case rewardsActivity
}

enum CustomerNotificationDeepLink {
    static let eventKeyField = "event_key"
    static let orderIdField = "orderId"
    static let disputeIdField = "disputeId"

    static func resolve(_ userInfo: [AnyHashable: Any]) -> CustomerNotificationDestination? {
        guard let eventKey = userInfo[eventKeyField] as? String else { return nil }
        return resolve(
            eventKey: eventKey,
            orderId: nonEmpty(userInfo[orderIdField]),
            disputeId: nonEmpty(userInfo[disputeIdField])
        )
    }

    static func resolve(
        eventKey: String,
        orderId: String?,
        disputeId: String?
    ) -> CustomerNotificationDestination? {
        switch eventKey {
        case "order.confirmed",
             "order.on_the_way",
             "order.in_progress",
             "order.completed",
             "order.cancelled",
             "order.refunded",
             "recurring.scheduled":
            guard let orderId else { return nil }
            return .order(orderId: orderId)
        case "dispute.reply":
            guard let disputeId else { return nil }
            return .dispute(disputeId: disputeId)
        case "membership.expiring_soon",
             "membership.cancellation_effective":
            return .subscribePlus
        case "loyalty.tier_upgrade":
            return .rewardsActivity
        default:
            return nil
        }
    }

    private static func nonEmpty(_ value: Any?) -> String? {
        (value as? String).flatMap { $0.isEmpty ? nil : $0 }
    }
}
