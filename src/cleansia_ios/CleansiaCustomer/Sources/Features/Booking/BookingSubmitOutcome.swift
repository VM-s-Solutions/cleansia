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
    /// The fresh quote refused the cash choice, so it was taken away and nothing was sent; the
    /// customer has already been told and chooses again.
    case paymentMethodCleared
    /// A cash booking by a customer with no usable card in the booking's currency: the card is captured
    /// in PaymentSheet's setup mode first, and `submitAfterCardGuarantee` books once it lands.
    case cardGuaranteeNeeded(PaymentSheetPresentation)
    /// The booking needs a card captured and the customer has not ticked the consent; nothing was sent.
    case cardGuaranteeConsentRequired
    /// PaymentSheet saved the card but the server does not hold it yet; nothing was sent, and the
    /// customer slides again.
    case cardGuaranteePending
}
