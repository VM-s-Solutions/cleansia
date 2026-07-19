import Foundation

enum PushTapRouting {
    struct Plan: Equatable {
        let selectOrdersTab: Bool
        let orderId: String?
        let selectEarningsTab: Bool
        let invoiceId: String?
    }

    static func plan(for destination: PartnerNotificationDestination) -> Plan {
        switch destination {
        case let .order(orderId):
            Plan(selectOrdersTab: true, orderId: orderId, selectEarningsTab: false, invoiceId: nil)
        case .ordersTab:
            Plan(selectOrdersTab: true, orderId: nil, selectEarningsTab: false, invoiceId: nil)
        case let .invoice(invoiceId):
            Plan(selectOrdersTab: false, orderId: nil, selectEarningsTab: true, invoiceId: invoiceId)
        case .earningsTab:
            Plan(selectOrdersTab: false, orderId: nil, selectEarningsTab: true, invoiceId: nil)
        }
    }

    static func deepLinkRoute(_ orderId: String?) -> OrderRoute? {
        orderId.map { .detail(orderId: $0) }
    }
}
