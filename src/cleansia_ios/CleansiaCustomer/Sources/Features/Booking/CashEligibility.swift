import Foundation

/// `BookingPolicy.AllowsCash`. `requiredEmployees` is the quote's crew for the selection on screen —
/// never assigned crew or spare seats — and nil while no quote describes that selection. The server
/// re-decides on create, so this only decides what the payment step offers.
enum CashEligibility: Equatable {
    case available
    case needsAccount
    case needsCard(requiredCleaners: Int)
    case needsSavedCard
    case cardCheckFailed
    case pending

    static let refusalCode = "order.cash_not_available"

    static func resolve(signedIn: Bool, requiredEmployees: Int?, hasUsableSavedCard: Bool?) -> CashEligibility {
        if let requiredEmployees, requiredEmployees > 1 {
            return .needsCard(requiredCleaners: requiredEmployees)
        }
        guard signedIn else { return .needsAccount }
        guard requiredEmployees == 1, let hasUsableSavedCard else { return .pending }
        return hasUsableSavedCard ? .available : .needsSavedCard
    }

    /// An unknown crew is not a refusal: a cash choice is kept until the crew is known.
    var refusesCash: Bool {
        switch self {
        case .needsAccount, .needsCard, .needsSavedCard: true
        case .available, .pending, .cardCheckFailed: false
        }
    }
}
