import CleansiaCustomerApi
import Foundation

enum OrdersFormat {
    /// Price + currency suffix (grouped, no fraction digits; CZK/EUR/USD/GBP get
    /// their symbol, others the raw code — never crashes on an unknown currency).
    static func price(_ amount: Double, currencyCode: String?) -> String {
        let code = currencyCode?.nonBlank ?? "CZK"
        let formatter = NumberFormatter()
        formatter.numberStyle = .decimal
        formatter.locale = .current
        formatter.maximumFractionDigits = 0
        formatter.minimumFractionDigits = 0
        let number = formatter.string(from: NSNumber(value: amount)) ?? "\(Int(amount))"
        switch code.uppercased() {
        case "CZK": return "\(number) Kč"
        case "EUR": return "\(number) €"
        case "USD": return "$\(number)"
        case "GBP": return "£\(number)"
        default: return "\(number) \(code)"
        }
    }

    /// "Mon 1 Jul · 10:00–12:00" — date + start–end window (`estimatedMinutes`
    /// drives the end). No estimate → just the start date-time. Weekday/month
    /// names render in `locale` (the app-selected language, not the device one).
    static func dateRange(_ date: Date?, estimatedMinutes: Int, locale: Locale = .current) -> String {
        guard let date else { return "—" }
        if estimatedMinutes <= 0 {
            return formatter(template: shortDateTimeTemplate, locale: locale).string(from: date)
        }
        let end = date.addingTimeInterval(Double(estimatedMinutes) * 60)
        let datePart = formatter(template: mediumDateTemplate, locale: locale).string(from: date)
        let time = formatter(template: timeOnlyTemplate, locale: locale)
        return "\(datePart) · \(time.string(from: date))–\(time.string(from: end))"
    }

    static func dateTime(_ date: Date?, locale: Locale = .current) -> String {
        guard let date else { return "—" }
        return formatter(template: shortDateTimeTemplate, locale: locale).string(from: date)
    }

    /// "Eco Products" from "eco_products"/"ecoProducts" (`prettifyExtraKey`).
    static func prettifyExtraKey(_ key: String) -> String {
        guard !key.isBlank else { return key }
        var spaced = key.replacingOccurrences(of: "_", with: " ")
            .replacingOccurrences(of: "-", with: " ")
        spaced = insertCamelSpaces(spaced)
        return spaced.split(separator: " ").map { word in
            word.prefix(1).uppercased() + word.dropFirst().lowercased()
        }.joined(separator: " ")
    }

    /// Up to 2 package names then service names, "+ N more" suffix. Names resolve
    /// to `locale`'s translation when the snapshot carries one, else the frozen
    /// English name.
    static func servicesSummary(_ order: OrderListItem, locale: Locale = .current) -> String {
        let languageCode = locale.language.languageCode?.identifier ?? "en"
        let packages = (order.selectedPackages ?? []).compactMap {
            localizedName($0.name, translations: $0.translations, languageCode: languageCode)
        }
        let services = (order.selectedServices ?? []).compactMap {
            localizedName($0.name, translations: $0.translations, languageCode: languageCode)
        }
        let combined = packages + services
        guard !combined.isEmpty else { return "—" }
        let shown = Array(combined.prefix(2))
        let remaining = combined.count - shown.count
        let base = shown.joined(separator: ", ")
        return remaining > 0 ? "\(base) \(L10n.Orders.servicesMore(remaining))" : base
    }

    private static func localizedName(
        _ fallback: String?,
        translations: [String: Translation]?,
        languageCode: String
    ) -> String? {
        translations?[languageCode]?.name?.nonBlank ?? fallback?.nonBlank
    }

    private static func insertCamelSpaces(_ text: String) -> String {
        var result = ""
        let chars = Array(text)
        for (index, char) in chars.enumerated() {
            if index > 0, char.isUppercase, chars[index - 1].isLowercase {
                result.append(" ")
            }
            result.append(char)
        }
        return result
    }

    private static let shortDateTimeTemplate = "EEE d MMM HH:mm"
    private static let mediumDateTemplate = "EEE d MMM"
    private static let timeOnlyTemplate = "HH:mm"

    private static let cacheLock = NSLock()
    private nonisolated(unsafe) static var formatterCache: [String: DateFormatter] = [:]

    private static func formatter(template: String, locale: Locale) -> DateFormatter {
        let key = "\(locale.identifier)|\(template)"
        cacheLock.lock()
        defer { cacheLock.unlock() }
        if let cached = formatterCache[key] {
            return cached
        }
        let formatter = DateFormatter()
        formatter.locale = locale
        formatter.setLocalizedDateFormatFromTemplate(template)
        formatterCache[key] = formatter
        return formatter
    }
}

private extension String {
    var nonBlank: String? {
        isBlank ? nil : self
    }
}
