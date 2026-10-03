import CleansiaCore
import SwiftUI

/// The Home → booking-sheet entry seams (the `MainShell.kt` openBooking /
/// prefillPackageId / rebookFromOrderId wiring plus the sheet-internal
/// hydration Android runs in `BookingBottomSheet.kt` — on iOS the sheet is
/// dumb, so the shell seeds the session-lived `BookingViewModel` up front).
extension CustomerShellView {
    /// The home top-bar selection feeds the booking hydration — the Android
    /// `preferred = selected ?? default ?? first` read (`BookingBottomSheet.kt:205-207`).
    var preferredAddress: SavedAddress? {
        HomeSections.displayedAddress(
            container.savedAddressRepository.addresses,
            selectedId: container.savedAddressRepository.selectedId
        )
    }

    /// The plain entries (FAB, hero CTAs, Orders empty-state) reopen the draft where it was left — step
    /// and selections — after hydrating its address — the sheet-internal
    /// `LaunchedEffect(visible, preferred?.id)` parity (`BookingBottomSheet.kt:270-282`). The draft
    /// lives in the session's view model; a booking placed or an entry that starts its own flow
    /// (a package, the quick-size slide, order again) replaces it.
    func openBooking() {
        bookingVM.update { BookingPrefill.hydratedWithPreferred($0, preferred: preferredAddress) }
        // A resumed draft's time may have passed while it was away. Only a plain open re-checks it: a seeded
        // one starts afresh, and a notice about a time it throws away would be noise.
        if bookingVM.revalidateResumedTime() {
            snackbar.showInfo(L10n.Booking.draftTimeChanged)
        }
        model.book()
    }

    func bookPackage(_ packageId: String) {
        bookingVM.reset()
        bookingVM.update {
            BookingPrefill.withPackage(
                BookingPrefill.hydratedWithPreferred($0, preferred: preferredAddress),
                packageId: packageId
            )
        }
        model.book()
    }

    /// The Home quick-size slide — the size it holds, on top of the usual address hydration.
    func bookSize(rooms: Int, bathrooms: Int) {
        bookingVM.reset()
        bookingVM.update {
            BookingPrefill.withSize(
                BookingPrefill.hydratedWithPreferred($0, preferred: preferredAddress),
                rooms: rooms,
                bathrooms: bathrooms
            )
        }
        model.book()
    }

    /// "Order again" — present first, then pre-fill from the fetched order
    /// (Android opens the sheet and lets the rebook effect fill it,
    /// `MainShell.kt:276-279` + `BookingBottomSheet.kt:305-374`). Rebook owns
    /// the address, so the preferred-address hydration is skipped.
    func rebookOrder(_ orderId: String) {
        bookingVM.reset()
        model.book()
        Task { await prefillRebook(orderId) }
    }

    private func prefillRebook(_ orderId: String) async {
        switch await container.orderClient.getById(orderId: orderId) {
        case let .failure(error):
            snackbar.showApiError(error)
        case let .success(order):
            let result = BookingPrefill.rebook(
                bookingVM.state,
                order: order,
                savedAddresses: container.savedAddressRepository.addresses,
                catalog: bookingVM.catalogState.loadedValue
            )
            bookingVM.update { _ in result.state }
            if result.droppedUnavailableItems {
                snackbar.showInfo(L10n.OrderDetail.rebookUnavailableItems)
            }
        }
    }
}
