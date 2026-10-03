import Foundation

/// The services step's own taps, split out of BookingViewModel.swift so the type stays under the lint
/// ceiling. A seeded selection (a Home package card, quick-size, Order again, a resumed draft) is
/// written through `update` and never asks.
extension BookingViewModel {
    /// Removes the service, or adds it — unless a selected package already includes it, when the
    /// customer is asked first (`twiceBookedPick`).
    func toggleService(_ id: String) {
        if !state.selectedServiceIds.contains(id),
           let pick = catalogState.loadedValue?.twiceBookedPick(
               addingService: id,
               selectedPackageIds: state.selectedPackageIds
           )
        {
            twiceBookedPick = pick
            return
        }
        update { var next = $0
            next.selectedServiceIds.formSymmetricDifference([id])
            return next
        }
    }

    /// Removes the package, or adds it — unless it includes a service already selected on its own,
    /// when the customer is asked first. True when the selection changed now.
    @discardableResult
    func togglePackage(_ id: String) -> Bool {
        if !state.selectedPackageIds.contains(id),
           let pick = catalogState.loadedValue?.twiceBookedPick(
               addingPackage: id,
               selectedServiceIds: state.selectedServiceIds
           )
        {
            twiceBookedPick = pick
            return false
        }
        update { var next = $0
            next.selectedPackageIds.formSymmetricDifference([id])
            return next
        }
        return true
    }

    func confirmTwiceBooked(_ pick: TwiceBookedPick) {
        twiceBookedPick = nil
        update { var next = $0
            switch pick {
            case let .service(id, _): next.selectedServiceIds.insert(id)
            case let .package(id, _): next.selectedPackageIds.insert(id)
            }
            return next
        }
    }

    func cancelTwiceBooked() {
        twiceBookedPick = nil
    }
}
