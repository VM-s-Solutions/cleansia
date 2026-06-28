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

    /// Appends the deep-link order detail to the Orders stack path. Returns the
    /// updated path so the binding-driven push is verifiable without a host.
    static func appendingDeepLink(_ orderId: String?, to path: [OrderRoute]) -> [OrderRoute] {
        guard let orderId else { return path }
        return path + [.detail(orderId: orderId)]
    }
}
