import CleansiaCore
import Foundation

struct PayPeriodProgress {
    let day: Int
    let total: Int

    var fraction: Double {
        Double(day) / Double(total)
    }
}

enum DashboardFormat {
    static func money(_ amount: Double, currencyCode: String?, fallback: String = "—") -> String {
        if amount <= 0 { return fallback }
        let rounded = roundedThousands(amount)
        guard let symbol = EarningsFormat.currencySymbol(currencyCode), !symbol.isEmpty else { return rounded }
        return "\(rounded) \(symbol)"
    }

    static func plainMoney(_ amount: Double, fallback: String = "—") -> String {
        if amount <= 0 { return fallback }
        return String(format: "%.0f", amount)
    }

    static func rating(_ value: Double?) -> String {
        guard let value else { return "—" }
        return String(format: "%.1f", value)
    }

    static func payoutDate(_ date: Date, locale: Locale = .current) -> String {
        let formatter = DateFormatter()
        formatter.locale = locale
        formatter.setLocalizedDateFormatFromTemplate("EEE d MMM")
        return formatter.string(from: date)
    }

    static func payPeriodProgress(start: Date, end: Date, now: Date = Date()) -> PayPeriodProgress {
        let calendar = Calendar.current
        let startDay = calendar.startOfDay(for: start)
        let endDay = calendar.startOfDay(for: end)
        let today = calendar.startOfDay(for: now)
        let totalDays = max((calendar.dateComponents([.day], from: startDay, to: endDay).day ?? 0) + 1, 1)
        let elapsed = (calendar.dateComponents([.day], from: startDay, to: today).day ?? 0) + 1
        let dayIndex = min(max(elapsed, 1), totalDays)
        return PayPeriodProgress(day: dayIndex, total: totalDays)
    }

    private static func roundedThousands(_ amount: Double) -> String {
        let formatter = NumberFormatter()
        formatter.numberStyle = .decimal
        formatter.maximumFractionDigits = 0
        formatter.groupingSeparator = "\u{202F}"
        return formatter.string(from: NSNumber(value: amount)) ?? String(format: "%.0f", amount)
    }
}
