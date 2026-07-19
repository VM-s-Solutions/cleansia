import Foundation

enum PartnerNotificationDestination: Equatable {
    case order(orderId: String)
    case ordersTab
    case invoice(invoiceId: String)
    case earningsTab
}

enum PartnerNotificationDeepLink {
    static let eventKeyField = "event_key"
    static let orderIdField = "orderId"
    static let invoiceIdField = "invoiceId"

    static func resolve(_ userInfo: [AnyHashable: Any]) -> PartnerNotificationDestination? {
        guard let eventKey = userInfo[eventKeyField] as? String else { return nil }
        let orderId = (userInfo[orderIdField] as? String).flatMap { $0.isEmpty ? nil : $0 }
        let invoiceId = (userInfo[invoiceIdField] as? String).flatMap { $0.isEmpty ? nil : $0 }
        return resolve(eventKey: eventKey, orderId: orderId, invoiceId: invoiceId)
    }

    static func resolve(
        eventKey: String,
        orderId: String?,
        invoiceId: String? = nil
    ) -> PartnerNotificationDestination? {
        switch eventKey {
        case "order.confirmed",
             "order.in_progress",
             "order.completed",
             "order.cancelled",
             "order.on_the_way",
             "order.assignment_cancelled",
             "dispute.reply":
            guard let orderId else { return nil }
            return .order(orderId: orderId)
        case "order.new_available":
            return .ordersTab
        case "payroll.invoice_paid":
            // Open the paid invoice; fall back to the Earnings tab when the
            // payload carries no invoiceId — parity with Android's
            // `InvoiceDetail(id) ?: Earnings` (the backend always sends one).
            guard let invoiceId else { return .earningsTab }
            return .invoice(invoiceId: invoiceId)
        default:
            return nil
        }
    }
}
