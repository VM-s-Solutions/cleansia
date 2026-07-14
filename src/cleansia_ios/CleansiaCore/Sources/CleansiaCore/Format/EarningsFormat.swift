import Foundation

public enum EarningsFormat {
    public static func decimalMoney(_ amount: Double, currencyCode: String?) -> String {
        money(amount, currencyCode: currencyCode, fractionDigits: 2)
    }

    public static func wholeMoney(_ amount: Double, currencyCode: String?) -> String {
        money(amount, currencyCode: currencyCode, fractionDigits: 0)
    }

    public static func currencySymbol(_ code: String?, locale: Locale = .current) -> String? {
        guard let code, !code.isEmpty else { return nil }
        // ISO gate: unknown codes fall back to the raw code (Android parity:
        // Currency.getInstance throws for unknown codes → raw-code fallback).
        guard Locale.commonISOCurrencyCodes.contains(code) else { return code }
        // Locale.Components, NOT identifier concatenation: device locales
        // often already carry keywords (e.g. "en_US@rg=czzzzz" when region
        // differs from language), and "…@rg=czzzzz@currency=CZK" is malformed
        // — the override is dropped and the symbol collapses to the base
        // locale's currency ("$").
        var components = Locale.Components(locale: locale)
        components.currency = Locale.Currency(code)
        return Locale(components: components).currencySymbol ?? code
    }

    public static func shortDate(_ date: Date?, locale: Locale = .current) -> String? {
        guard let date else { return nil }
        let formatter = DateFormatter()
        formatter.locale = locale
        formatter.timeZone = .current
        formatter.setLocalizedDateFormatFromTemplate("d MMM yyyy")
        return formatter.string(from: date)
    }

    private static func money(
        _ amount: Double,
        currencyCode: String?,
        fractionDigits: Int
    ) -> String {
        let number = grouped(amount, fractionDigits: fractionDigits)
        guard let symbol = currencySymbol(currencyCode), !symbol.isEmpty else { return number }
        return "\(number) \(symbol)"
    }

    private static func grouped(_ amount: Double, fractionDigits: Int) -> String {
        let formatter = NumberFormatter()
        formatter.numberStyle = .decimal
        formatter.locale = .current
        formatter.minimumFractionDigits = fractionDigits
        formatter.maximumFractionDigits = fractionDigits
        formatter.groupingSeparator = "\u{202F}"
        return formatter.string(from: NSNumber(value: amount))
            ?? String(format: "%.\(fractionDigits)f", amount)
    }
}
