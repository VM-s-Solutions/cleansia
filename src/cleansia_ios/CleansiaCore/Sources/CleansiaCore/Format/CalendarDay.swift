import Foundation

/// A birth date is a day, not a moment, but it travels through the app as a `Date`. This is the one
/// instant the app means by a day — midnight UTC — and the one zone it reads and shows one in.
///
/// The invariant matters because two things produce such a `Date` and they do not agree on their own:
/// a value decoded off the wire arrives as midnight UTC, while a value taken from a picker is whatever
/// time of day that picker happened to be carrying. Normalize at the picker and both are the same
/// instant, so the day survives a round trip instead of moving one square west of Greenwich per save.
public enum CalendarDay {
    /// The device's own calendar system, pinned to Greenwich, so a picker offers — and a formatter
    /// prints — the day the instant denotes rather than the day it happens to fall on locally.
    public static var calendar: Calendar {
        var calendar = Calendar.current
        calendar.timeZone = .gmt
        return calendar
    }

    /// Midnight UTC of the day `date` falls on in `timeZone`. No default zone on purpose: the caller
    /// always knows which calendar the value was read in, and guessing is the whole of this bug.
    public static func startOfDay(_ date: Date, in timeZone: TimeZone) -> Date {
        var source = Calendar(identifier: .gregorian)
        source.timeZone = timeZone
        var greenwich = Calendar(identifier: .gregorian)
        greenwich.timeZone = .gmt
        let parts = source.dateComponents([.year, .month, .day], from: date)
        return greenwich.date(from: parts) ?? date
    }

    public static func text(_ date: Date, locale: Locale) -> String {
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.locale = locale
        formatter.timeZone = .gmt
        return formatter.string(from: date)
    }
}
