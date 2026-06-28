import Foundation

enum BookingSubmitOutcome: Equatable {
    case success(orderId: String, confirmationCode: String)
    case cardPending(orderId: String, confirmationCode: String, payment: PaymentSheetParams)
    case failed
    case profileIncomplete
}

struct PaymentSheetParams: Equatable {
    let clientSecret: String
    let ephemeralKey: String
    let customerId: String
}
