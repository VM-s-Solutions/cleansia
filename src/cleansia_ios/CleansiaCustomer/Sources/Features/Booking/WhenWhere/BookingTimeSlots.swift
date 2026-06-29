import Foundation

enum SlotState: Equatable {
    case available
    case express
    case unavailable
    case earliest
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

enum BookingTimeSlots {
    static let firstWindowHour = 8
    static let lastWindowHour = 20
    static let expressLeadHours = 2.0
    static let standardLeadHours = 4.0

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

    static func slots(for date: Date, now: Date = Date(), calendar: Calendar = .current) -> [BookingTimeSlot] {
        let isToday = calendar.isDate(date, inSameDayAs: now)
        var earliestAssigned = false

        return (firstWindowHour ... lastWindowHour - 1).map { hour in
            let label = String(format: "%02d:00", hour)
            guard isToday else { return BookingTimeSlot(time: label, state: .available) }

            guard let slotInstant = instant(date: date, timeLabel: label, calendar: calendar) else {
                return BookingTimeSlot(time: label, state: .unavailable)
            }
            let leadHours = slotInstant.timeIntervalSince(now) / 3600.0
            let state: SlotState
            if leadHours < expressLeadHours {
                state = .unavailable
            } else if leadHours < standardLeadHours {
                state = .express
            } else if !earliestAssigned {
                earliestAssigned = true
                state = .earliest
            } else {
                state = .available
            }
            return BookingTimeSlot(time: label, state: state)
        }
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
