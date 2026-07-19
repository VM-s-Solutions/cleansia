import Foundation

enum ProfileStatsFormat {
    /// "%.0f Kč" style, mirroring Android's profile formatter; symbol-less when
    /// the user has no realized orders (currency null).
    static func saved(_ amount: Double, currencyCode: String?) -> String {
        let number = String(format: "%.0f", amount)
        guard let code = currencyCode else { return number }
        let symbol = switch code.uppercased() {
        case "CZK": "Kč"
        case "EUR": "€"
        case "USD": "$"
        default: code
        }
        return "\(number) \(symbol)"
    }

    /// Account-creation date → localized "MMM yyyy" (e.g. "Feb 2025"); em dash
    /// if unknown. The locale is required (not defaulted to `.current`) so the
    /// caller passes the app-selected language, which the device locale ignores.
    static func memberSince(_ date: Date?, locale: Locale) -> String {
        guard let date else { return "—" }
        let formatter = DateFormatter()
        formatter.locale = locale
        formatter.setLocalizedDateFormatFromTemplate("MMM y")
        return formatter.string(from: date)
    }
}
