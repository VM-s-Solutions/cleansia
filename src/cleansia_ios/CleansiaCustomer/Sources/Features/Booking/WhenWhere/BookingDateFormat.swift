import CleansiaCore
import Foundation

enum BookingDateFormat {
    static func dayLabel(_ date: Date, calendar: Calendar = .current, now: Date = Date()) -> String {
        if calendar.isDate(date, inSameDayAs: now) {
            return L10n.Booking.today
        }
        let formatter = DateFormatter()
        formatter.calendar = calendar
        formatter.locale = CoreL10n.locale
        formatter.setLocalizedDateFormatFromTemplate("EEE")
        return formatter.string(from: date)
    }
}
