import CleansiaCustomerApi
import Foundation

/// Pure seeding of the session-lived `BookingState` from the Home surfaces —
/// the `BookingBottomSheet.kt` hydration/prefill/rebook effects (:270-282,
/// :305-374, :390-399), lifted out so the shell wiring stays logic-free.
enum BookingPrefill {
    /// Fill the address fields from the preferred saved address, only when the
    /// user hasn't typed one (`state.street.isBlank()` — the Android guard).
    /// The Android copy also assigns `countryIsoCode`, but server-loaded saved
    /// addresses carry an empty ISO there; iOS keeps the current value —
    /// `savedAddressId` drives the submit either way.
    static func hydratedWithPreferred(_ state: BookingState, preferred: SavedAddress?) -> BookingState {
        guard let preferred, state.street.isBlank else { return state }
        var next = state
        next.street = preferred.street
        next.city = preferred.city
        next.zipCode = preferred.zipCode
        next.savedAddressId = preferred.id
        return next
    }

    /// Popular-package tap → union into the selection; everything else keeps
    /// its defaults so the user only changes what they want.
    static func withPackage(_ state: BookingState, packageId: String) -> BookingState {
        var next = state
        next.selectedPackageIds.insert(packageId)
        return next
    }

    /// "Order again" → replay a past order into the draft. Retired catalog
    /// items are dropped (and flagged for the snackbar) only when the catalog
    /// has loaded; a cold catalog trusts the original ids and lets the create
    /// call fail loudly later.
    static func rebook(
        _ state: BookingState,
        order: OrderItem,
        savedAddresses: [SavedAddress],
        catalog: Catalog?
    ) -> (state: BookingState, droppedUnavailableItems: Bool) {
        let matchedSaved = order.address.flatMap { address in
            savedAddresses.first {
                $0.street.caseInsensitiveEquals(address.street) &&
                    $0.city.caseInsensitiveEquals(address.city) &&
                    $0.zipCode.caseInsensitiveEquals(address.zipCode)
            }
        }

        let originalServiceIds = (order.selectedServices ?? []).compactMap(\.id)
        let originalPackageIds = (order.selectedPackages ?? []).compactMap(\.id)

        let activeServiceIds = Set((catalog?.services ?? []).map(\.id))
        let activePackageIds = Set((catalog?.packages ?? []).map(\.id))
        let catalogReady = !activeServiceIds.isEmpty || !activePackageIds.isEmpty

        let keptServiceIds = catalogReady
            ? originalServiceIds.filter(activeServiceIds.contains)
            : originalServiceIds
        let keptPackageIds = catalogReady
            ? originalPackageIds.filter(activePackageIds.contains)
            : originalPackageIds
        let droppedAny = catalogReady &&
            (keptServiceIds.count != originalServiceIds.count || keptPackageIds.count != originalPackageIds.count)

        var next = state
        next.selectedServiceIds = Set(keptServiceIds)
        next.selectedPackageIds = Set(keptPackageIds)
        if let rooms = order.rooms, rooms > 0 { next.rooms = rooms }
        if let bathrooms = order.bathrooms, bathrooms > 0 { next.bathrooms = bathrooms }
        next.street = order.address?.street ?? ""
        next.city = order.address?.city ?? ""
        next.zipCode = order.address?.zipCode ?? ""
        next.savedAddressId = matchedSaved?.id
        return (next, droppedAny)
    }
}

private extension String {
    func caseInsensitiveEquals(_ other: String?) -> Bool {
        caseInsensitiveCompare(other ?? "") == .orderedSame
    }
}
