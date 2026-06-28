import Foundation

enum PartnerNotificationDestination: Equatable {
    case order(orderId: String)
    case ordersTab
}

enum PartnerNotificationDeepLink {
    static let eventKeyField = "event_key"
    static let orderIdField = "orderId"

    static func resolve(_ userInfo: [AnyHashable: Any]) -> PartnerNotificationDestination? {
        guard let eventKey = userInfo[eventKeyField] as? String else { return nil }
        let orderId = (userInfo[orderIdField] as? String).flatMap { $0.isEmpty ? nil : $0 }
        return resolve(eventKey: eventKey, orderId: orderId)
    }

    static func resolve(eventKey: String, orderId: String?) -> PartnerNotificationDestination? {
        switch eventKey {
        case "order.confirmed",
             "order.in_progress",
             "order.completed",
             "order.cancelled",
             "order.on_the_way",
             "dispute.reply":
            guard let orderId else { return nil }
            return .order(orderId: orderId)
        case "order.new_available":
            return .ordersTab
        default:
            return nil
        }
    }
}
