import Foundation

enum RecurrenceFrequency: Int, CaseIterable {
    case weekly = 1
    case biweekly = 2
    case monthly = 3
}

/// A schedule's start, as the "HH:mm" the server reads in the market's zone. The server books only the
/// quarter-hours from 08:00 to 19:45, the one-off booking window.
enum RecurringTime {
    static let defaultTime = "10:00"

    static let bookableTimes: [String] = stride(
        from: BookingTimeSlots.firstWindowHour * 60,
        to: BookingTimeSlots.lastWindowHour * 60,
        by: BookingTimeSlots.bookingSlotIntervalMinutes
    ).map { label(minutes: $0) }

    /// A start outside the window lands on its nearest edge; one that cannot be read on the default.
    static func nearestBookable(_ time: String) -> String {
        let parts = time.split(separator: ":")
        guard parts.count >= 2, let hour = Int(parts[0]), let minute = Int(parts[1]) else { return defaultTime }
        let step = BookingTimeSlots.bookingSlotIntervalMinutes
        let rounded = Int((Double(hour * 60 + minute) / Double(step)).rounded()) * step
        let first = BookingTimeSlots.firstWindowHour * 60
        let last = BookingTimeSlots.lastWindowHour * 60 - step
        return label(minutes: min(max(rounded, first), last))
    }

    static func format(_ date: Date) -> String {
        let components = Calendar.current.dateComponents([.hour, .minute], from: date)
        return String(format: "%02d:%02d", components.hour ?? 0, components.minute ?? 0)
    }

    /// Foundation weekday: Sun=1..Sat=7. Backend wants .NET DayOfWeek: Sun=0..Sat=6.
    static func dotNetDayOfWeek(_ date: Date) -> Int {
        let weekday = Calendar.current.component(.weekday, from: date)
        return (weekday - 1) % 7
    }

    private static func label(minutes: Int) -> String {
        String(format: "%02d:%02d", minutes / 60, minutes % 60)
    }
}

/// The backend's `PaymentType` as the schedule commands carry it.
enum RecurringPaymentType {
    static let cash = 1
    static let card = 2
}

struct RecurringTemplate: Equatable, Identifiable {
    let id: String
    let frequency: Int
    let dayOfWeek: Int
    let timeOfDay: String
    let rooms: Int
    let bathrooms: Int
    let savedAddressId: String
    let addressLine: String?
    let selectedServiceIds: [String]
    let selectedPackageIds: [String]
    let paymentType: Int
    let startsOn: Date
    let endsOn: Date?
    let preferredEmployeeId: String?
    let isActive: Bool
    /// A cash schedule whose selection now needs more than one cleaner: the server skips it rather than
    /// switching it to card, so it books nothing until the customer changes it.
    let requiresPaymentMethodChange: Bool
}

/// The card's status badge. A paused schedule says so whatever else is true of it.
enum RecurringStatusBadge: Equatable {
    case paused
    case needsPaymentChange

    static func of(_ template: RecurringTemplate) -> RecurringStatusBadge? {
        if !template.isActive { return .paused }
        return template.requiresPaymentMethodChange ? .needsPaymentChange : nil
    }
}

struct CreateRecurringInput: Equatable {
    let frequency: Int
    let dayOfWeek: Int
    let timeOfDay: String
    let rooms: Int
    let bathrooms: Int
    let savedAddressId: String
    let selectedServiceIds: [String]
    let selectedPackageIds: [String]
    let paymentType: Int
    let startsOn: Date
    let preferredEmployeeId: String?
}

/// `UpdateRecurringBooking` replaces every field it is sent, `EndsOn` and `PreferredEmployeeId` included,
/// so an edit carries the template's end date forward and sends the form's favourite cleaner rather than
/// letting a nil clear them.
struct UpdateRecurringInput: Equatable {
    let templateId: String
    let frequency: Int
    let dayOfWeek: Int
    let timeOfDay: String
    let rooms: Int
    let bathrooms: Int
    let savedAddressId: String
    let selectedServiceIds: [String]
    let selectedPackageIds: [String]
    let paymentType: Int
    let startsOn: Date
    let endsOn: Date?
    let preferredEmployeeId: String?
}

struct RecurringSavedAddress: Equatable, Identifiable {
    let id: String
    let label: String?
    let street: String?
    let city: String?
    /// The market the schedule is priced in: the catalogue the form offers follows this country.
    let countryId: String?
    let isDefault: Bool

    var displayLine: String {
        [street, city].compactMap { $0 }.filter { !$0.isEmpty }.joined(separator: ", ")
    }
}
