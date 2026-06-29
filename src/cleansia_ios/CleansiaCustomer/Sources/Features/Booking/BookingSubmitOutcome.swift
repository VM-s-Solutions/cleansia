import Foundation

enum BookingSubmitOutcome: Equatable {
    case success(orderId: String, confirmationCode: String)
    case cardPending(orderId: String, confirmationCode: String)
    case failed
    case profileIncomplete
}
