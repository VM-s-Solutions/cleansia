import CleansiaCore
import CleansiaPartnerApi
import Foundation

struct PayPeriodProgress {
    let day: Int
    let total: Int

    var fraction: Double {
        Double(day) / Double(total)
    }
}

enum DashboardFormat {
    static func money(
        _ amount: Double,
        currencyCode: String?,
        fallback: String = "—",
        locale: Locale = .current
    ) -> String {
        if amount <= 0 { return fallback }
        let rounded = roundedThousands(amount)
        guard let symbol = EarningsFormat.currencySymbol(currencyCode, locale: locale), !symbol.isEmpty else {
            return rounded
        }
        return "\(rounded) \(symbol)"
    }

    static func nextJobLabel(_ status: OrderStatus) -> String {
        switch status {
        case ._4: L10n.Dashboard.inProgress
        case ._3: L10n.Dashboard.onTheWay
        default: L10n.Dashboard.nextJob
        }
    }

    /// Android's `nextJobWhenLine` rules; the day text follows the iOS localized-template convention rather
    /// than Android's fixed "EEE d MMM" pattern.
    static func nextJobWhen(_ startsAt: Date?, now: Date = Date(), locale: Locale = .current) -> String {
        guard let startsAt else { return "—" }
        let calendar = Calendar.current
        let time = formatted(startsAt, template: "HH:mm", locale: locale)
        let seconds = startsAt.timeIntervalSince(now)
        if seconds >= 0 {
            let minutes = Int(seconds / 60)
            let hours = minutes / 60
            if minutes < 60 { return L10n.Dashboard.urgencyInMinutes(minutes) }
            if hours < 24 {
                if calendar.isDate(startsAt, inSameDayAs: now) { return "\(L10n.Orders.dayToday) \(time)" }
                return L10n.Dashboard.urgencyInHoursMinutes(hours, minutes % 60)
            }
            let tomorrow = calendar.date(byAdding: .day, value: 1, to: now)
            if let tomorrow, calendar.isDate(startsAt, inSameDayAs: tomorrow) {
                return "\(L10n.Orders.dayTomorrow) \(time)"
            }
        }
        let sameYear = calendar.component(.year, from: startsAt) == calendar.component(.year, from: now)
        let day = formatted(startsAt, template: sameYear ? "EEE d MMM" : "d MMM yyyy", locale: locale)
        return "\(day) · \(time)"
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

    private static func formatted(_ date: Date, template: String, locale: Locale) -> String {
        let formatter = DateFormatter()
        formatter.locale = locale
        formatter.setLocalizedDateFormatFromTemplate(template)
        return formatter.string(from: date)
    }
}
