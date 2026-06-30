import Foundation

enum SubscribeOutcome: Equatable {
    case needsPaymentMethod(PaymentSheetPresentation)
    case subscribed(membershipId: String)
    case alreadyActive
    case failed
}
