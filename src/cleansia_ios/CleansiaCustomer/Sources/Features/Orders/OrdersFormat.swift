import CleansiaCustomerApi
import Foundation

enum OrdersFormat {
    /// Price + currency suffix, grouped; CZK/EUR/USD/GBP get their symbol, others the raw code — never
    /// crashes on an unknown currency. A blank code renders the bare amount: an unlabelled figure over a
    /// label guessed for it. Every customer money row reads this, the booking flow's included.
    ///
    /// A whole amount prints without a fraction ("1,200 Kč") and any other to the currency's minor unit
    /// ("319.90 Kč"), the web's `formatMoney` and Android's `formatOrderPrice` rule: a credit share or a
    /// discount can leave haléře, and rounding them away shows a figure the card is not charged (Stripe's
    /// sheet shows the decimals). Under half a minor unit from a whole number is whole (319.999 is
    /// "320 Kč"); a blank or unknown code takes two digits.
    static func price(_ amount: Double, currencyCode: String?, locale: Locale = .current) -> String {
        let rounded = amount.rounded()
        let isWhole = abs(amount - rounded) < 0.005
        let digits = isWhole ? 0 : minorUnits(currencyCode)
        let formatter = NumberFormatter()
        formatter.numberStyle = .decimal
        formatter.locale = locale
        formatter.maximumFractionDigits = digits
        formatter.minimumFractionDigits = digits
        // `rounded == 0` folds -0.0 (from -0.001) into 0, which would otherwise print "-0".
        let value = isWhole ? (rounded == 0 ? 0 : rounded) : amount
        let number = formatter.string(from: NSNumber(value: value)) ?? "\(Int(value))"
        guard let code = currencyCode?.nonBlank else { return number }
        switch code.uppercased() {
        case "CZK": return "\(number) Kč"
        case "EUR": return "\(number) €"
        case "USD": return "$\(number)"
        case "GBP": return "£\(number)"
        default: return "\(number) \(code)"
        }
    }

    /// The currency's own minor unit — none for yen; two for a blank or unknown code.
    private static func minorUnits(_ currencyCode: String?) -> Int {
        guard let code = currencyCode?.nonBlank?.uppercased(), Locale.commonISOCurrencyCodes.contains(code) else {
            return 2
        }
        let formatter = NumberFormatter()
        formatter.numberStyle = .currency
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.currencyCode = code
        return formatter.maximumFractionDigits
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
    static func servicesSummary(_ order: CustomerOrderSummary, locale: Locale = .current) -> String {
        let languageCode = locale.language.languageCode?.identifier ?? "en"
        let packages = order.packages.compactMap {
            localizedName($0.name, translations: $0.translations, languageCode: languageCode)
        }
        let services = order.services.compactMap {
            localizedName($0.name, translations: $0.translations, languageCode: languageCode)
        }
        let combined = packages + services
        guard !combined.isEmpty else { return "—" }
        let shown = Array(combined.prefix(2))
        let remaining = combined.count - shown.count
        let base = shown.joined(separator: ", ")
        return remaining > 0 ? "\(base) \(L10n.Orders.servicesMore(remaining))" : base
    }

    /// Order-DETAIL catalog line name resolved to `locale`'s snapshot translation
    /// when the order carries one, else the frozen English snapshot name (or "—").
    /// Same resolution the order-list `servicesSummary` uses, exposed for the
    /// order-detail service/package cards.
    static func localizedCatalogName(
        _ fallback: String?,
        translations: [String: Translation]?,
        locale: Locale = .current
    ) -> String {
        let languageCode = locale.language.languageCode?.identifier ?? "en"
        return localizedName(fallback, translations: translations, languageCode: languageCode) ?? "—"
    }

    static func localizedCatalogDescription(
        _ fallback: String?,
        translations: [String: Translation]?,
        locale: Locale = .current
    ) -> String? {
        let languageCode = locale.language.languageCode?.identifier ?? "en"
        return translations?[languageCode]?.description?.nonBlank ?? fallback?.nonBlank
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
