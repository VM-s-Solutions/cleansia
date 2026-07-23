import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct ResolvedOrderInputs {
    let profile: BookingProfile
    let quote: BookingQuote
    let instant: Date
    let countryId: String?
    let promoIsValid: Bool
}

enum BookingOrderCommandFactory {
    static func make(state: BookingState, resolved: ResolvedOrderInputs) -> CreateOrderCommand {
        let inlineAddress = state.savedAddressId == nil
            ? AddressDto(
                street: state.street,
                city: state.city,
                zipCode: state.zipCode,
                countryId: resolved.countryId
            )
            : nil

        let promo: String? = {
            guard resolved.promoIsValid, !state.promoCode.isBlank else { return nil }
            return state.promoCode.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        }()

        return CreateOrderCommand(
            customerName: resolved.profile.fullName,
            customerEmail: resolved.profile.email,
            customerPhone: resolved.profile.phoneNumber,
            customerAddress: inlineAddress,
            savedAddressId: state.savedAddressId,
            selectedPackageIds: state.selectedPackageIds.sorted(),
            selectedServiceIds: state.selectedServiceIds.sorted(),
            rooms: state.rooms,
            bathrooms: state.bathrooms,
            extras: state.selectedExtraSlugs.reduce(into: [:]) { $0[$1] = true },
            cleaningDate: resolved.instant,
            paymentType: (state.paymentMethod ?? .cash).paymentType,
            currencyId: resolved.quote.currencyId.isBlank ? nil : resolved.quote.currencyId,
            totalPrice: resolved.quote.totalPrice,
            promoCode: promo,
            referralCode: nil,
            preferredEmployeeId: state.preferredEmployeeId
        )
    }
}
