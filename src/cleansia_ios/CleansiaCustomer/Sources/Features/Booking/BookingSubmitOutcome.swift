import CleansiaCore
import Foundation

enum BookingSubmitOutcome: Equatable {
    case success(orderId: String, confirmationCode: String)
    case cardPending(orderId: String, confirmationCode: String, presentation: PaymentSheetPresentation)
    /// Carries the server's `ApiError` whenever the failure came from a response,
    /// so the sheet can surface the specific, already-translated business message
    /// (`error.order.no_available_spots`, `error.order.time_conflict`,
    /// `error.city.not_serviced`, …) instead of one generic network toast.
    ///
    /// `nil` means there was no server response to report — the local guards
    /// (double tap, no session, no date chosen, a payment intent that came back
    /// successful but empty). Those keep the generic message.
    ///
    /// The payload is deliberately part of the outcome rather than a
    /// `SnackbarController` injected into `BookingViewModel`: the VM holds only
    /// clients + a scheduler, and the sheet already owns the snackbar host.
    case failed(ApiError?)
    case profileIncomplete
}
