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

    /// Given the customer, PaymentSheet draws its own save box on an intent that keeps nothing, and a card
    /// saved through it gets no SavedCards row and no consent. So the customer rides only on an intent the
    /// server set up to save the card.
    static func cardPayment(
        clientSecret: String,
        ephemeralKey: String,
        stripeCustomerId: String,
        intentSavesCard: Bool
    ) -> PaymentSheetPresentation {
        PaymentSheetPresentation(
            clientSecret: clientSecret,
            ephemeralKey: intentSavesCard ? ephemeralKey : "",
            stripeCustomerId: intentSavesCard ? stripeCustomerId : "",
            merchantDisplayName: "Cleansia",
            intentKind: .payment
        )
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
