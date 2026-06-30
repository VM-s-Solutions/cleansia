import Foundation

enum PaymentIntentKind: Equatable {
    case payment
    case setup
}

struct PaymentSheetPresentation: Equatable, CustomStringConvertible, CustomDebugStringConvertible {
    let clientSecret: String
    let ephemeralKey: String
    let stripeCustomerId: String
    let merchantDisplayName: String
    let intentKind: PaymentIntentKind

    init(
        clientSecret: String,
        ephemeralKey: String,
        stripeCustomerId: String,
        merchantDisplayName: String,
        intentKind: PaymentIntentKind = .payment
    ) {
        self.clientSecret = clientSecret
        self.ephemeralKey = ephemeralKey
        self.stripeCustomerId = stripeCustomerId
        self.merchantDisplayName = merchantDisplayName
        self.intentKind = intentKind
    }

    var description: String {
        "PaymentSheetPresentation(merchant: \(merchantDisplayName), kind: \(intentKind), secrets: <redacted>)"
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
