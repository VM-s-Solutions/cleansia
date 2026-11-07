import CleansiaCore
import Foundation

/// The partner half of the backend's audience keysets (D2): everything the
/// partner mobile host serves into the feed; customer keys never reach this host.
enum PartnerFeedEventKeys {
    static let all: Set<String> = [
        "order.new_available",
        "order.seat_open",
        "order.reminder_tomorrow",
        "order.preferred_offer",
        "order.assignment_cancelled",
        "payroll.invoice_paid"
    ]

    static func contains(_ eventKey: String) -> Bool {
        all.contains(eventKey)
    }
}

/// Renders feed rows from the SAME `push.<event_key>.title|body` catalog the
/// APNs display resolves (ADR-0025), in the app language. An unknown event key
/// hides the row — the push side's `templateFor(...) ?: return` drop-parity.
enum NotificationFeedTemplates {
    static func rows(from items: [UserNotification]) -> [NotificationFeedRow] {
        items.compactMap { item in
            guard let rendered = render(eventKey: item.eventKey, args: item.args) else { return nil }
            return NotificationFeedRow(
                id: item.id,
                title: rendered.title,
                body: rendered.body,
                createdOn: item.createdOn,
                isUnread: item.readOn == nil
            )
        }
    }

    static func render(eventKey: String, args: [String: String]) -> (title: String, body: String)? {
        guard PartnerFeedEventKeys.contains(eventKey) else { return nil }
        return (L10n.localized("push.\(eventKey).title"), body(eventKey: eventKey, args: args))
    }

    private static func body(eventKey: String, args: [String: String]) -> String {
        switch eventKey {
        case "order.new_available", "order.reminder_tomorrow":
            String(format: L10n.localized("push.\(eventKey).body"), args["count"] ?? "")
        case "order.assignment_cancelled", "order.preferred_offer", "order.seat_open":
            String(format: L10n.localized("push.\(eventKey).body"), args["orderNumber"] ?? "")
        default:
            L10n.localized("push.\(eventKey).body")
        }
    }
}
