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

    /// The period's ends are the server's calendar days, each midnight UTC, and today is the phone's own date
    /// pinned the same way, so no time zone moves a day.
    static func payPeriodProgress(
        start: Date,
        end: Date,
        now: Date = Date(),
        timeZone: TimeZone = .current
    ) -> PayPeriodProgress {
        let totalDays = max(days(from: start, to: end) + 1, 1)
        let elapsed = days(from: start, to: CalendarDay.startOfDay(now, in: timeZone)) + 1
        return PayPeriodProgress(day: min(max(elapsed, 1), totalDays), total: totalDays)
    }

    static func days(from start: Date, to end: Date) -> Int {
        CalendarDay.calendar.dateComponents([.day], from: start, to: end).day ?? 0
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
