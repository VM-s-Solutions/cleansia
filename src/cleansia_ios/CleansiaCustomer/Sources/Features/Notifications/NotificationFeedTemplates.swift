import Foundation

/// The customer half of the backend's audience keysets (D2): everything the
/// customer mobile host serves into the feed. `promo.new_sitewide` is excluded
/// from feed v1 (Q-FEED-01) and partner keys never reach this host.
enum CustomerFeedEventKeys {
    static let all: Set<String> = [
        "order.confirmed",
        "order.on_the_way",
        "order.in_progress",
        "order.completed",
        "order.cancelled",
        "order.refunded",
        "dispute.reply",
        "recurring.scheduled",
        "membership.expiring_soon",
        "membership.cancellation_effective",
        "loyalty.tier_upgrade"
    ]

    static func contains(_ eventKey: String) -> Bool {
        all.contains(eventKey)
    }
}

struct NotificationFeedRow: Equatable, Identifiable {
    let id: String
    let title: String
    let body: String
    let createdOn: Date
    let isUnread: Bool
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
        guard CustomerFeedEventKeys.contains(eventKey) else { return nil }
        return (L10n.localized("push.\(eventKey).title"), body(eventKey: eventKey, args: args))
    }

    private static func body(eventKey: String, args: [String: String]) -> String {
        switch eventKey {
        case "loyalty.tier_upgrade":
            // Feed rendering is programmatic (D5/FCH-5), so unlike the argless
            // APNs body it can show the tier from `args.tier` — Android parity.
            L10n.NotificationsInbox.loyaltyTierBody(tierLabel(args["tier"]))
        case _ where orderNumberEvents.contains(eventKey):
            String(format: L10n.localized("push.\(eventKey).body"), args["orderNumber"] ?? "")
        default:
            L10n.localized("push.\(eventKey).body")
        }
    }

    private static let orderNumberEvents: Set<String> = [
        "order.confirmed",
        "order.on_the_way",
        "order.in_progress",
        "order.completed",
        "order.cancelled",
        "order.refunded",
        "recurring.scheduled"
    ]

    private static func tierLabel(_ enumName: String?) -> String {
        switch enumName {
        case "BronzeCleaner": L10n.localized("loyalty_tier_bronze_cleaner")
        case "SilverMopper": L10n.localized("loyalty_tier_silver_mopper")
        case "GoldPolisher": L10n.localized("loyalty_tier_gold_polisher")
        case "PlatinumSparkler": L10n.localized("loyalty_tier_platinum_sparkler")
        default: enumName ?? ""
        }
    }
}

enum NotificationFeedFormat {
    /// "now" / "2 hours ago" / "yesterday" within the last week, then the
    /// absolute `OrdersFormat.dateTime` — both rendering in the app locale.
    static func timestamp(_ date: Date, relativeTo now: Date = Date(), locale: Locale) -> String {
        let clamped = min(date, now)
        guard now.timeIntervalSince(clamped) < 7 * 24 * 3600 else {
            return OrdersFormat.dateTime(clamped, locale: locale)
        }
        let formatter = RelativeDateTimeFormatter()
        formatter.locale = locale
        formatter.dateTimeStyle = .named
        return formatter.localizedString(for: clamped, relativeTo: now)
    }
}
