import Foundation

public enum EarningsFormat {
    public static func decimalMoney(_ amount: Double, currencyCode: String?) -> String {
        money(amount, currencyCode: currencyCode, fractionDigits: 2)
    }

    public static func wholeMoney(_ amount: Double, currencyCode: String?) -> String {
        money(amount, currencyCode: currencyCode, fractionDigits: 0)
    }

    public static func currencySymbol(_ code: String?) -> String? {
        guard let code, !code.isEmpty else { return nil }
        // Locale's @currency override resolves an UNKNOWN code to the current
        // locale's default symbol, not the code — so gate on the ISO list and
        // fall back to the raw code when it isn't recognized (Android parity:
        // Currency.getInstance throws for unknown codes → raw-code fallback).
        guard Locale.commonISOCurrencyCodes.contains(code) else { return code }
        let locale = Locale(identifier: "\(Locale.current.identifier)@currency=\(code)")
        return locale.currencySymbol ?? code
    }

    public static func shortDate(_ date: Date?) -> String? {
        guard let date else { return nil }
        let formatter = DateFormatter()
        formatter.locale = .current
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
