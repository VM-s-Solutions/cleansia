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
    /// The resolved id of the address's country, the market every quote and catalogue read is made
    /// in. A saved address carries its own; an inline one resolves from `countryIsoCode` once picked.
    var countryId: String?
    var savedAddressId: String?
    /// The saved-address id the last preferred-address hydration seeded. Lets a
    /// re-open re-hydrate a still-auto-hydrated draft when the user's preferred
    /// selection changed, while leaving a hand-picked address untouched
    /// (`BookingBottomSheet.kt:270-282` reset-then-hydrate parity).
    var hydratedFromSavedId: String?
    var selectedDate: String = ""
    var selectedTime: String = ""
    var selectedInstant: Date?

    var paymentMethod: PaymentMethod?
    var specialInstructions: String = ""
    var accessInstructions: String = ""
    var promoCode: String = ""
    var referralCode: String = ""

    var preferredEmployeeId: String?
}
