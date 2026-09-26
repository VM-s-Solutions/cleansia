import Foundation

enum RecurrenceFrequency: Int, CaseIterable {
    case weekly = 1
    case biweekly = 2
    case monthly = 3
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
}

/// `UpdateRecurringBooking` replaces every field it is sent, `EndsOn` included, so an edit carries the
/// template's existing end date forward rather than letting a nil clear it.
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
