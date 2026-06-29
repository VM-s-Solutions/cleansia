import Foundation

enum BookingSubmitOutcome: Equatable {
    case success(orderId: String, confirmationCode: String)
    case cardPending(orderId: String, confirmationCode: String, presentation: PaymentSheetPresentation)
    case failed
    case profileIncomplete
}
