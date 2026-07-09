import CleansiaCustomerApi
import Foundation

/// The Home tab's section predicates and row mappers, lifted out of the view
/// as pure logic (`HomeTab.kt:108-215` + `:971-978`). Each function cites the
/// Android line it mirrors so parity review is line-to-line.
enum HomeSections {
    /// `selected ?? default ?? first` (`HomeTab.kt:111-113`).
    static func displayedAddress(_ addresses: [SavedAddress], selectedId: String?) -> SavedAddress? {
        addresses.first { $0.id == selectedId }
            ?? addresses.first { $0.isDefault }
            ?? addresses.first
    }

    /// Non-blank ids, top 3 in catalog order (`HomeTab.kt:149-153`). The raw
    /// package is preserved (name + translations intact) so the card localizes
    /// reactively at render via `CatalogPackage.localizedName(for:)` — the same
    /// catalog localization the booking sheet uses (`ServicesStep.kt:104-118`),
    /// which also lets a live language switch re-resolve the title.
    static func popularPackages(_ packages: [CatalogPackage]) -> [CatalogPackage] {
        Array(packages.filter { !$0.id.isEmpty }.prefix(3))
    }

    /// Active templates, top 3 (`HomeTab.kt:163-165`).
    static func activeRecurring(_ templates: [RecurringTemplate]) -> [RecurringTemplate] {
        Array(templates.filter(\.isActive).prefix(3))
    }

    /// First Completed order in repo (recent-first) order (`HomeTab.kt:170-172`).
    static func mostRecentCompleted(_ orders: [OrderListItem]) -> OrderListItem? {
        orders.first { OrderStatusGroup.isCompleted($0.status) }
    }

    /// Defensive local sort by cleaningDateTime desc, nils last, top 3
    /// (`HomeTab.kt:177-181`).
    static func recentForDisplay(_ orders: [OrderListItem]) -> [OrderListItem] {
        let sorted = orders.sorted {
            ($0.cleaningDateTime ?? .distantPast) > ($1.cleaningDateTime ?? .distantPast)
        }
        return Array(sorted.prefix(3))
    }

    /// Render-gate for the recent block (`HomeTab.kt:185`).
    static func showRecent(recent: [OrderListItem], ordersLoaded: Bool, ordersLoading: Bool) -> Bool {
        !recent.isEmpty && (ordersLoaded || !ordersLoading)
    }

    /// Milestone card gate — hidden for guests/top tier (`HomeTab.kt:295-300`).
    static func showMilestone(_ account: LoyaltyAccount?) -> Bool {
        guard let account else { return false }
        return account.nextTier != nil && account.pointsToNextTier != nil
    }

    /// First-paint skeleton gate (`HomeTab.kt:196-203`).
    static func firstPaintReady(ordersLoaded: Bool, membershipReady: Bool, packagesReady: Bool) -> Bool {
        ordersLoaded && membershipReady && packagesReady
    }

    /// First service name, then first package name, "+ N more" suffix
    /// (`recentBookingTitle`, `HomeTab.kt:971-978`). Line names resolve to
    /// `languageCode`'s snapshot translation when present, else the frozen
    /// English name, and the suffix routes through the same localized key as
    /// the Orders-tab summary (`OrdersFormat.servicesSummary`) so both render
    /// wholly in the app language.
    static func recentBookingTitle(_ order: OrderListItem, fallback: String, languageCode: String) -> String {
        let serviceNames = (order.selectedServices ?? []).compactMap {
            localizedLineName($0.name, translations: $0.translations, languageCode: languageCode)
        }
        let packageNames = (order.selectedPackages ?? []).compactMap {
            localizedLineName($0.name, translations: $0.translations, languageCode: languageCode)
        }
        let names = serviceNames + packageNames
        guard let first = names.first else { return fallback }
        let remaining = names.count - 1
        return remaining > 0 ? "\(first) \(L10n.Orders.servicesMore(remaining))" : first
    }

    private static func localizedLineName(
        _ fallback: String?,
        translations: [String: Translation]?,
        languageCode: String
    ) -> String? {
        translations?[languageCode]?.name?.nonBlank ?? fallback?.nonBlank
    }

    /// Mapped status label, else the wire name, else nil → chip hidden
    /// (`HomeTab.kt:1021-1023`).
    static func statusChipLabel(_ order: OrderListItem) -> String? {
        if let status = order.status {
            return L10n.Orders.statusLabel(status)
        }
        return order.orderStatus?.name?.nonBlank
    }

    /// "MMM d" for the Order-again subtitle, nil-safe (`HomeTab.kt:684-692`).
    static func orderAgainWhen(_ date: Date?) -> String? {
        guard let date else { return nil }
        return orderAgainFormatter.string(from: date)
    }

    private static let orderAgainFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = .current
        formatter.setLocalizedDateFormatFromTemplate("MMM d")
        return formatter
    }()
}

private extension String {
    var nonBlank: String? {
        isBlank ? nil : self
    }
}
