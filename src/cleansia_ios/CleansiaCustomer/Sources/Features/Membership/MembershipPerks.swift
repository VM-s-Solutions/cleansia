import Foundation

/// The perks an active membership actually unlocks, as semantic cases rather than resolved strings, so
/// the card and its test name the same thing.
///
/// Express upgrade is deliberately absent — here, on the subscribe screen and on the success screen.
/// The plan carries `allowsExpressUpgrade` and the API returns it, but no pricing code reads it —
/// `BookingPolicy.RequiresExpressSurcharge` is lead-time only (2-4h ahead, not "same day"), so a member
/// pays the standard surcharge. Advertising it would promise something the product does not deliver.
enum MembershipPerk: Equatable, Identifiable {
    case discount(percent: Int)
    case freeCancellation(hours: Int)
    case recurring

    var id: String {
        switch self {
        case .discount: "discount"
        case .freeCancellation: "freeCancellation"
        case .recurring: "recurring"
        }
    }

    var systemImage: String {
        switch self {
        case .discount: "tag"
        case .freeCancellation: "clock"
        case .recurring: "repeat"
        }
    }

    var label: String {
        switch self {
        case let .discount(percent): L10n.Membership.perkPillDiscount(percent)
        case let .freeCancellation(hours): L10n.Membership.perkPillCancellation(hours)
        case .recurring: L10n.Membership.perkPillRecurring
        }
    }
}

enum MembershipPerks {
    static func resolve(_ membership: MyMembership) -> [MembershipPerk] {
        guard membership.hasMembership else { return [] }
        var perks: [MembershipPerk] = []
        if let percent = membership.discountPercentage.map({ Int($0) }), percent > 0 {
            perks.append(.discount(percent: percent))
        }
        if let hours = membership.freeCancellationWindowHours, hours > 0 {
            perks.append(.freeCancellation(hours: hours))
        }
        // Recurring templates are gated on an active membership and nothing else, so membership itself
        // is the backing condition — there is no per-plan flag to read.
        perks.append(.recurring)
        return perks
    }
}
