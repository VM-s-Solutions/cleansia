import Foundation

enum SlotState: Equatable {
    case available
    case express
    case unavailable
}

struct BookingDay: Equatable, Identifiable {
    let date: Date
    let weekdayIndex: Int
    let dayNumber: Int
    let isToday: Bool

    var id: Date {
        date
    }
}

struct BookingTimeSlot: Equatable, Identifiable {
    let time: String
    let state: SlotState

    var id: String {
        time
    }
}

/// The part of day the time step asks for first — the web wizard's `dayParts`: each holds the arrival
/// times whose hour falls in its range, sixteen quarter hours apiece. → /customer-app/ordering-flow#step-2-date-time
enum DayPart: CaseIterable, Equatable {
    case morning
    case afternoon
    case evening

    var hours: Range<Int> {
        switch self {
        case .morning: BookingTimeSlots.firstWindowHour ..< 12
        case .afternoon: 12 ..< 16
        case .evening: 16 ..< BookingTimeSlots.lastWindowHour
        }
    }

    /// The part an "HH:mm" arrival falls in; nil outside the booking window or for an unreadable time.
    static func of(_ time: String) -> DayPart? {
        guard let hour = time.split(separator: ":").first.flatMap({ Int($0) }) else { return nil }
        return allCases.first { $0.hours.contains(hour) }
    }
}

/// One part's slots. A part with nothing bookable left — today's morning, by noon — is drawn disabled.
struct DayPartSlots: Equatable {
    let part: DayPart
    let slots: [BookingTimeSlot]

    var bookableCount: Int {
        slots.filter { $0.state != .unavailable }.count
    }

    var isBookable: Bool {
        bookableCount > 0
    }
}

enum BookingTimeSlots {
    static let firstWindowHour = 8
    static let lastWindowHour = 20
    static let bookingSlotIntervalMinutes = 15

    static func days(now: Date = Date(), calendar: Calendar = .current) -> [BookingDay] {
        let today = calendar.startOfDay(for: now)
        return (0 ... 7).compactMap { offset in
            guard let date = calendar.date(byAdding: .day, value: offset, to: today) else { return nil }
            return BookingDay(
                date: date,
                weekdayIndex: calendar.component(.weekday, from: date),
                dayNumber: calendar.component(.day, from: date),
                isToday: offset == 0
            )
        }
    }

    /// Every quarter hour from 08:00 to 19:45. On today's date one inside the 2 h lead is unavailable (drawn
    /// greyed and disabled) and one in the 2–4 h band is express; every other date offers them all.
    static func slots(for date: Date, now: Date = Date(), calendar: Calendar = .current) -> [BookingTimeSlot] {
        let isToday = calendar.isDate(date, inSameDayAs: now)

        return stride(
            from: firstWindowHour * 60,
            to: lastWindowHour * 60,
            by: bookingSlotIntervalMinutes
        ).map { minutes in
            let label = String(format: "%02d:%02d", minutes / 60, minutes % 60)
            guard isToday else { return BookingTimeSlot(time: label, state: .available) }

            guard let slotInstant = instant(date: date, timeLabel: label, calendar: calendar) else {
                return BookingTimeSlot(time: label, state: .unavailable)
            }
            let leadHours = slotInstant.timeIntervalSince(now) / 3600.0
            let state: SlotState = if leadHours < BookingPricing.expressLeadHours {
                .unavailable
            } else if BookingPricing.requiresExpressSurcharge(cleaningAt: slotInstant, now: now) {
                .express
            } else {
                .available
            }
            return BookingTimeSlot(time: label, state: state)
        }
    }

    static func dayParts(_ slots: [BookingTimeSlot]) -> [DayPartSlots] {
        DayPart.allCases.map { part in DayPartSlots(part: part, slots: slots.filter { DayPart.of($0.time) == part }) }
    }

    /// The part the step opens on: the one holding the booked time, else the first with a bookable slot.
    /// The web always holds a time (09:00, or the date's first bookable slot), which lands on the same part.
    static func openingDayPart(_ parts: [DayPartSlots], selectedTime: String) -> DayPart {
        if let held = DayPart.of(selectedTime), parts.contains(where: { $0.part == held && $0.isBookable }) {
            return held
        }
        return parts.first(where: \.isBookable)?.part ?? .morning
    }

    static func instant(date: Date, timeLabel: String, calendar: Calendar = .current) -> Date? {
        let parts = timeLabel.split(separator: ":")
        guard parts.count == 2, let hour = Int(parts[0]), let minute = Int(parts[1]) else { return nil }
        var components = calendar.dateComponents([.year, .month, .day], from: date)
        components.hour = hour
        components.minute = minute
        components.second = 0
        return calendar.date(from: components)
    }
}
