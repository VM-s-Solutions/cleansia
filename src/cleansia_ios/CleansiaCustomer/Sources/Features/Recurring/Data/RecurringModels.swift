import Foundation

enum RecurrenceFrequency: Int, CaseIterable {
    case weekly = 1
    case biweekly = 2
    case monthly = 3
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

struct RecurringSavedAddress: Equatable, Identifiable {
    let id: String
    let label: String?
    let street: String?
    let city: String?
    let isDefault: Bool

    var displayLine: String {
        [street, city].compactMap { $0 }.filter { !$0.isEmpty }.joined(separator: ", ")
    }
}
