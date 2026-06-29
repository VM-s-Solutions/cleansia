import Foundation

struct PaymentSheetPresentation: Equatable, CustomStringConvertible, CustomDebugStringConvertible {
    let clientSecret: String
    let ephemeralKey: String
    let stripeCustomerId: String
    let merchantDisplayName: String

    var description: String {
        "PaymentSheetPresentation(merchant: \(merchantDisplayName), secrets: <redacted>)"
    }

    var debugDescription: String {
        description
    }
}

enum PaymentSheetOutcome: Equatable {
    case completed
    case canceled
    case failed
}

@MainActor
protocol PaymentSheetPresenting: AnyObject {
    func present(_ presentation: PaymentSheetPresentation) async -> PaymentSheetOutcome
}
