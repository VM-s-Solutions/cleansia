import Foundation

enum PushTapRouting {
    struct Plan: Equatable {
        let selectOrdersTab: Bool
        let orderId: String?
    }

    static func plan(for destination: PartnerNotificationDestination) -> Plan {
        switch destination {
        case let .order(orderId):
            Plan(selectOrdersTab: true, orderId: orderId)
        case .ordersTab:
            Plan(selectOrdersTab: true, orderId: nil)
        }
    }

    static func deepLinkRoute(_ orderId: String?) -> OrderRoute? {
        orderId.map { .detail(orderId: $0) }
    }
}
