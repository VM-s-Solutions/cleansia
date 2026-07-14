import CleansiaPartnerApi
import Foundation

enum OrdersFormat {
    static func pay(_ order: OrderListItem) -> String {
        money(order.estimatedCleanerPay ?? 0, symbol: order.currency?.symbol)
    }

    static func totalEarnings(_ orders: [OrderListItem]) -> String {
        let total = orders.reduce(0.0) { $0 + ($1.estimatedCleanerPay ?? 0) }
        return money(total, symbol: commonSymbol(orders))
    }

    static func money(_ amount: Double, symbol: String?) -> String {
        let rounded = Int(amount.rounded())
        let formatter = NumberFormatter()
        formatter.numberStyle = .decimal
        formatter.maximumFractionDigits = 0
        formatter.groupingSeparator = "\u{202F}"
        let whole = formatter.string(from: NSNumber(value: rounded)) ?? "\(rounded)"
        guard let symbol = symbol?.trimmingCharacters(in: .whitespaces), !symbol.isEmpty else { return whole }
        return "\(whole) \(symbol)"
    }

    static func sortLabel(_ sort: AvailableSort) -> String {
        switch sort {
        case .earningsHighToLow: L10n.Orders.sortEarningsHighToLow
        case .soonestFirst: L10n.Orders.sortSoonestFirst
        case .priceHighToLow: L10n.Orders.sortPriceHighToLow
        }
    }

    static func periodLabel(_ period: CompletedPeriod) -> String {
        switch period {
        case .thisWeek: L10n.Orders.periodThisWeek
        case .thisMonth: L10n.Orders.periodThisMonth
        case .lastMonth: L10n.Orders.periodLastMonth
        case .all: L10n.Orders.periodAll
        }
    }

    static func dayBucketLabel(_ bucket: ActiveDayBucket) -> String {
        switch bucket {
        case .today: L10n.Orders.dayToday
        case .tomorrow: L10n.Orders.dayTomorrow
        case .later: L10n.Orders.dayLater
        }
    }

    static func rooms(_ count: Int) -> String {
        L10n.Orders.rooms(count)
    }

    static func baths(_ count: Int) -> String {
        L10n.Orders.baths(count)
    }

    static func extras(_ count: Int) -> String {
        L10n.Orders.extras(count)
    }

    static func addressLine(_ order: OrderListItem, distanceKm: Double?) -> String? {
        let address = nonBlank(order.customerAddress) ?? nonBlank(order.customerAddressApproximate)
        let distance = distanceKm.map { L10n.Orders.kmAway(distanceString($0)) }
        let parts = [address, distance].compactMap { $0 }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }

    static func compactSubtitle(_ order: OrderListItem) -> String {
        nonBlank(order.customerAddress) ?? nonBlank(order.customerName) ?? L10n.Orders.guest
    }

    static func bannerTitle(_ order: OrderListItem) -> String {
        nonBlank(order.customerName)
            ?? nonBlank(order.customerAddress)
            ?? "#\(order.displayOrderNumber ?? "")"
    }

    /// Weekday/month names render in `locale` (the app-selected language, not the
    /// device one); "Today"/"Tomorrow" prefixes still resolve from the L10n bundle.
    static func relativeDateTime(_ date: Date?, locale: Locale = .current) -> String {
        guard let date else { return "—" }
        let calendar = Calendar.current
        let time = templateFormatter("HH:mm", locale: locale).string(from: date)
        if calendar.isDateInToday(date) { return "\(L10n.Orders.dayToday) \(time)" }
        if calendar.isDateInTomorrow(date) { return "\(L10n.Orders.dayTomorrow) \(time)" }
        return templateFormatter("EEE d MMM HH:mm", locale: locale).string(from: date)
    }

    static func timeOnly(_ date: Date?, locale: Locale = .current) -> String {
        guard let date else { return "—" }
        return templateFormatter("HH:mm", locale: locale).string(from: date)
    }

    static func dayHeader(_ date: Date?, locale: Locale = .current) -> String {
        guard let date else { return L10n.Orders.unscheduled }
        return mediumDateFormatter(locale: locale).string(from: date)
    }

    private static func distanceString(_ kilometres: Double) -> String {
        kilometres < 1 ? String(format: "%.1f", kilometres) : "\(Int(kilometres.rounded()))"
    }

    private static func commonSymbol(_ orders: [OrderListItem]) -> String? {
        let symbols = Set(orders.compactMap { $0.currency?.symbol })
        return symbols.count == 1 ? symbols.first : nil
    }

    private static func nonBlank(_ value: String?) -> String? {
        guard let value, !value.trimmingCharacters(in: .whitespaces).isEmpty else { return nil }
        return value
    }

    private static let cacheLock = NSLock()
    private nonisolated(unsafe) static var templateCache: [String: DateFormatter] = [:]
    private nonisolated(unsafe) static var mediumDateCache: [String: DateFormatter] = [:]

    private static func templateFormatter(_ template: String, locale: Locale) -> DateFormatter {
        let key = "\(locale.identifier)|\(template)"
        cacheLock.lock()
        defer { cacheLock.unlock() }
        if let cached = templateCache[key] { return cached }
        let formatter = DateFormatter()
        formatter.locale = locale
        formatter.setLocalizedDateFormatFromTemplate(template)
        templateCache[key] = formatter
        return formatter
    }

    private static func mediumDateFormatter(locale: Locale) -> DateFormatter {
        cacheLock.lock()
        defer { cacheLock.unlock() }
        if let cached = mediumDateCache[locale.identifier] { return cached }
        let formatter = DateFormatter()
        formatter.locale = locale
        formatter.dateStyle = .medium
        mediumDateCache[locale.identifier] = formatter
        return formatter
    }
}
