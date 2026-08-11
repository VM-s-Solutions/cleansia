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
             "order.assigned",
             // Still the order detail even though the cleaner is off the job: the copy says the job
             // moved, and the detail is where they read which day just came off their schedule.
             "order.assignment_revoked",
             // Stays on the DETAIL rather than the pending-offers surface, because the push fires on
             // a wider predicate than the reservation does: it is produced from the resolver's
             // recipient, while a hold is granted only when the resolver also returned a deadline.
             // Below an eight-hour lead there is a recipient and no reservation, and a card order is
             // pushed before its payment lands, so in both cases the offers list — `hold > now`
             // conjoined with offerability — would be empty. The detail carries the disclosure and
             // the decline instead, and degrades to an ordinary job where no hold exists.
             "order.preferred_offer",
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
