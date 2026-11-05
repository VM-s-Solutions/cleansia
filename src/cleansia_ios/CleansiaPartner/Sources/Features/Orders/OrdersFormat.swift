import CleansiaCore
import CleansiaPartnerApi
import Foundation

enum OrdersFormat {
    /// One figure per currency on the board, joined with the list's own separator: a EUR job on a
    /// Czech cleaner's board is still counted, it is just never added into a figure labelled Kč.
    /// Orders with no currency form their own unlabelled group. Grouped by code, not symbol — the
    /// symbol is display only — and ordered by first appearance so the dominant currency leads.
    static func totalEarnings(_ orders: [OrderListItem]) -> String {
        var codes: [String?] = []
        var totals: [String?: (amount: Double, symbol: String?)] = [:]
        for order in orders {
            let code = order.currency?.code
            if totals[code] == nil {
                codes.append(code)
                totals[code] = (0, symbolOf(order.currency))
            }
            totals[code]?.amount += order.estimatedCleanerPay ?? 0
        }
        if codes.isEmpty { return money(0, symbol: nil) }
        return codes
            .compactMap { totals[$0] }
            .map { money($0.amount, symbol: $0.symbol) }
            .joined(separator: " · ")
    }

    /// Thousands grouped, the symbol after; no unit when there is none. A whole amount prints without a
    /// fraction and any other to two decimals in the locale's mark — the customer app's and Android's
    /// rule, so a job's pay reads the same on the board, the offer, the contract and the detail. Pay is
    /// booked to the haléř, and a seat's share of a job need not be whole: 412.30 is "412,30 Kč", never
    /// "412 Kč". Only the symbol reaches this function, so the minor unit is two digits: the koruna's and
    /// the euro's. Under half a haléř from whole is whole. The mark is the in-app language's, not the phone's:
    /// the switch never moves `Locale.current`, so Czech on an English phone would print "746.93 Kč".
    static func money(_ amount: Double, symbol: String?, locale: Locale = CoreL10n.locale) -> String {
        let rounded = amount.rounded()
        let isWhole = abs(amount - rounded) < 0.005
        let formatter = NumberFormatter()
        formatter.numberStyle = .decimal
        formatter.locale = locale
        formatter.minimumFractionDigits = isWhole ? 0 : 2
        formatter.maximumFractionDigits = isWhole ? 0 : 2
        formatter.groupingSeparator = "\u{202F}"
        // `rounded == 0` folds -0.0 (from -0.001) into 0, which would otherwise print "-0".
        let value = isWhole ? (rounded == 0 ? 0 : rounded) : amount
        let number = formatter.string(from: NSNumber(value: value)) ?? "\(value)"
        guard let symbol = symbol?.trimmingCharacters(in: .whitespaces), !symbol.isEmpty else { return number }
        return "\(number) \(symbol)"
    }

    /// The server's symbol, else the one its currency code names (Android's `symbolOf`): a blank symbol never leaves
    /// a figure without its unit.
    static func symbolOf(symbol: String?, code: String?) -> String? {
        if let symbol = symbol?.trimmingCharacters(in: .whitespaces), !symbol.isEmpty { return symbol }
        return EarningsFormat.currencySymbol(code)
    }

    static func symbolOf(_ currency: CurrencyListItem?) -> String? {
        symbolOf(symbol: currency?.symbol, code: currency?.code)
    }

    static func sortLabel(_ sort: AvailableSort) -> String {
        switch sort {
        case .soonest: L10n.Orders.sortSoonestFirst
        case .bestPay: L10n.Orders.sortEarningsHighToLow
        }
    }

    static func rooms(_ count: Int) -> String {
        L10n.Orders.rooms(count)
    }

    static func baths(_ count: Int) -> String {
        L10n.Orders.baths(count)
    }

    /// The board card flags only a dirtier-than-normal home; the detail names every level.
    static func dirtinessChip(_ level: DirtinessLevel?) -> String? {
        level == ._0 ? nil : L10n.Orders.dirtinessLevel(level)
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

    /// The job's arrival window as the contract states it: "22 Apr · 10:00–12:00"; the start alone
    /// when the estimate is not positive (the `formatOrderDateRange` parity).
    static func window(_ start: Date, minutes: Int, locale: Locale = .current) -> String {
        let day = templateFormatter("d MMM", locale: locale).string(from: start)
        return "\(day) · \(timeWindow(start, minutes: minutes, locale: locale))"
    }

    /// The window's time part alone, "09:00–13:00"; the start alone when the estimate is not positive.
    static func timeWindow(_ start: Date, minutes: Int, locale: Locale = .current) -> String {
        let from = templateFormatter("HH:mm", locale: locale).string(from: start)
        guard minutes > 0 else { return from }
        let end = start.addingTimeInterval(TimeInterval(minutes) * 60)
        return "\(from)–\(templateFormatter("HH:mm", locale: locale).string(from: end))"
    }

    /// "Sat 1 Nov" — the locale's best pattern for the skeleton, so the month takes the form a day number needs.
    static func shortDay(_ date: Date, locale: Locale = .current) -> String {
        templateFormatter("EEEdMMM", locale: locale).string(from: date)
    }

    /// "Sat 1 Nov, 14:00".
    static func shortDayTime(_ date: Date, locale: Locale = .current) -> String {
        "\(shortDay(date, locale: locale)), \(timeOnly(date, locale: locale))"
    }

    /// "1 November".
    static func longDay(_ date: Date, locale: Locale = .current) -> String {
        templateFormatter("dMMMM", locale: locale).string(from: date)
    }

    /// "16 Nov", in the locale's order: the date beside a weekday on one line.
    static func dayMonth(_ date: Date, locale: Locale = .current) -> String {
        templateFormatter("dMMM", locale: locale).string(from: date)
    }

    /// "Saturday", capitalised in every language: it stands on its own, at the start of a line or as an item.
    static func wideWeekday(_ date: Date, locale: Locale = .current) -> String {
        let weekday = templateFormatter("EEEE", locale: locale).string(from: date)
        return weekday.prefix(1).uppercased(with: locale) + weekday.dropFirst()
    }

    /// "Friday, November 13": the platform's long date with its weekday.
    static func weekdayLongDay(_ date: Date, locale: Locale = .current) -> String {
        templateFormatter("EEEEdMMMM", locale: locale).string(from: date)
    }

    /// A tile's text: the standalone abbreviated month ("LLL", never the genitive "MMM"), the day, the weekday.
    static func pattern(_ pattern: String, _ date: Date, locale: Locale = .current) -> String {
        let key = "pattern|\(pattern)"
        return cached(key, locale: locale) { $0.dateFormat = pattern }.string(from: date)
    }

    /// A dated instant with its time: "22 Apr 2026 · 10:00" (the `formatOrderDateTime` parity).
    static func dateTime(_ date: Date, locale: Locale = .current) -> String {
        let day = templateFormatter("d MMM yyyy", locale: locale).string(from: date)
        return "\(day) · \(templateFormatter("HH:mm", locale: locale).string(from: date))"
    }

    private static let cacheLock = NSLock()
    private nonisolated(unsafe) static var templateCache: [String: DateFormatter] = [:]
    private nonisolated(unsafe) static var mediumDateCache: [String: DateFormatter] = [:]

    private static func templateFormatter(_ template: String, locale: Locale) -> DateFormatter {
        cached(template, locale: locale) { $0.setLocalizedDateFormatFromTemplate(template) }
    }

    private static func cached(
        _ name: String,
        locale: Locale,
        configure: (DateFormatter) -> Void
    ) -> DateFormatter {
        let key = "\(locale.identifier)|\(name)"
        cacheLock.lock()
        defer { cacheLock.unlock() }
        if let cached = templateCache[key] { return cached }
        let formatter = DateFormatter()
        formatter.locale = locale
        configure(formatter)
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
