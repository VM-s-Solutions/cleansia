import Foundation

enum ProfileStatsFormat {
    /// Every money row's formatter (Android's profile reads `formatOrderPrice` too); symbol-less when
    /// the user has no realized orders (currency null).
    static func saved(_ amount: Double, currencyCode: String?) -> String {
        OrdersFormat.price(amount, currencyCode: currencyCode)
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
