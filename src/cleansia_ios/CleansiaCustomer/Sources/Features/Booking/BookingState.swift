import Foundation

struct BookingState: Equatable {
    var selectedServiceIds: Set<String> = []
    var selectedPackageIds: Set<String> = []
    var selectedExtraSlugs: Set<String> = []
    var rooms: Int = 1
    var bathrooms: Int = 1

    var street: String = ""
    var city: String = ""
    var zipCode: String = ""
    var countryIsoCode: String = ""
    var savedAddressId: String?
    var selectedDate: String = ""
    var selectedTime: String = ""
    var selectedInstant: Date?

    var paymentMethod: String = ""
    var specialInstructions: String = ""
    var promoCode: String = ""
    var referralCode: String = ""

    var preferredEmployeeId: String?
}
