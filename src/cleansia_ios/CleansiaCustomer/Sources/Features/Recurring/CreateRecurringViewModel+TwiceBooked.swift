import Foundation

/// The services section's own taps, split out of CreateRecurringViewModel.swift so the file stays under
/// the lint ceiling. A prefilled order and an edited template seed the selection without asking.
extension CreateRecurringViewModel {
    /// Removes the service, or adds it — unless a selected package already includes it, when the
    /// customer is asked first (`twiceBookedPick`).
    func toggleService(_ id: String) {
        if !formState.selectedServiceIds.contains(id),
           let pick = catalogState.loadedValue?.twiceBookedPick(
               addingService: id,
               selectedPackageIds: formState.selectedPackageIds
           )
        {
            twiceBookedPick = pick
            return
        }
        formState.selectedServiceIds.formSymmetricDifference([id])
    }

    /// Removes the package, or adds it — unless it includes a service already selected on its own,
    /// when the customer is asked first.
    func togglePackage(_ id: String) {
        if !formState.selectedPackageIds.contains(id),
           let pick = catalogState.loadedValue?.twiceBookedPick(
               addingPackage: id,
               selectedServiceIds: formState.selectedServiceIds
           )
        {
            twiceBookedPick = pick
            return
        }
        formState.selectedPackageIds.formSymmetricDifference([id])
    }

    func confirmTwiceBooked(_ pick: TwiceBookedPick) {
        twiceBookedPick = nil
        switch pick {
        case let .service(id, _): formState.selectedServiceIds.insert(id)
        case let .package(id, _): formState.selectedPackageIds.insert(id)
        }
    }

    func cancelTwiceBooked() {
        twiceBookedPick = nil
    }
}
