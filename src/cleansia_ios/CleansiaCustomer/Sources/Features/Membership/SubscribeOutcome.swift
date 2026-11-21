import Foundation

enum SubscribeOutcome: Equatable {
    case needsPaymentMethod(PaymentSheetPresentation)
    case needsAuthentication(paymentIntentClientSecret: String)
    case subscribed(membershipId: String)
    case alreadyActive
    case failed
}
