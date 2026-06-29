import Foundation

enum BookingCardResolution: Equatable {
    case navigateToSuccess(confirmationCode: String)
    case snackbar(messageKey: String)
}

enum BookingCardResultResolver {
    static func resolve(_ outcome: PaymentSheetOutcome, confirmationCode: String) -> BookingCardResolution {
        switch outcome {
        case .completed:
            .navigateToSuccess(confirmationCode: confirmationCode)
        case .canceled:
            .snackbar(messageKey: "error_payment_cancelled")
        case .failed:
            .snackbar(messageKey: "error_payment_failed")
        }
    }
}
