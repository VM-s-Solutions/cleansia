import Foundation

/// What a customer surface may say about the express surcharge waiver. A member inside the free trial
/// is entitled like a paying one, so the server's count is theirs to read.
enum ExpressWaiverStatus: Equatable {
    case none
    case available
    case exhausted

    var isAdvertised: Bool {
        self != .none
    }

    static func resolve(
        hasMembership: Bool,
        upgradesPerMonth: Int?,
        upgradesRemaining: Int?
    ) -> ExpressWaiverStatus {
        // `upgradesPerMonth` null IS the non-member state by the server's own definition, so folding
        // it onto zero here lands on exactly the answer it means.
        guard hasMembership, (upgradesPerMonth ?? 0) > 0 else { return .none }
        // `upgradesRemaining` is not the same: null means *no membership* and zero means *used up*, so
        // collapsing the two tells a member on a quota plan they spent a benefit they paid for. Nothing
        // here can tell which it is, and silence is the only answer that is not a claim.
        guard let upgradesRemaining else { return .none }
        return upgradesRemaining > 0 ? .available : .exhausted
    }

    static func resolve(_ membership: MyMembership?) -> ExpressWaiverStatus {
        guard let membership else { return .none }
        return resolve(
            hasMembership: membership.hasMembership && !membership.benefitsPaused,
            upgradesPerMonth: membership.expressUpgradesPerMonth,
            upgradesRemaining: membership.expressUpgradesRemaining
        )
    }

    static func resolve(_ snapshot: MembershipSnapshot?) -> ExpressWaiverStatus {
        guard let snapshot else { return .none }
        return resolve(
            hasMembership: snapshot.hasMembership && !snapshot.benefitsPaused,
            upgradesPerMonth: snapshot.expressUpgradesPerMonth,
            upgradesRemaining: snapshot.expressUpgradesRemaining
        )
    }
}

/// The express states a perk row can actually render, so the label switch stays total —
/// `ExpressWaiverStatus.none` is an absent perk, not a perk with nothing to say.
enum MembershipExpressPerk: Equatable {
    case available(remaining: Int)
    case exhausted

    init?(status: ExpressWaiverStatus, remaining: Int) {
        switch status {
        case .none: return nil
        case .available: self = .available(remaining: remaining)
        case .exhausted: self = .exhausted
        }
    }
}

/// The perks an active membership actually unlocks, as semantic cases rather than resolved strings, so
/// the card and its test name the same thing.
enum MembershipPerk: Equatable, Identifiable {
    case discount(percent: Int)
    case freeCancellation(hours: Int)
    case recurring
    case express(MembershipExpressPerk)

    var id: String {
        switch self {
        case .discount: "discount"
        case .freeCancellation: "freeCancellation"
        case .recurring: "recurring"
        case .express: "express"
        }
    }

    var systemImage: String {
        switch self {
        case .discount: "tag"
        case .freeCancellation: "clock"
        case .recurring: "repeat"
        case .express: "bolt"
        }
    }

    var label: String {
        switch self {
        case let .discount(percent): L10n.Membership.perkPillDiscount(percent)
        case let .freeCancellation(hours): L10n.Membership.perkPillCancellation(hours)
        case .recurring: L10n.Membership.perkPillRecurring
        case let .express(state):
            switch state {
            case let .available(remaining): L10n.Membership.perkPillExpress(remaining)
            case .exhausted: L10n.Membership.perkPillExpressUsed
            }
        }
    }
}

enum MembershipPerks {
    static func resolve(_ membership: MyMembership) -> [MembershipPerk] {
        guard membership.hasMembership, !membership.benefitsPaused else { return [] }
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
        if let express = MembershipExpressPerk(
            status: ExpressWaiverStatus.resolve(membership),
            remaining: membership.expressUpgradesRemaining ?? 0
        ) {
            perks.append(.express(express))
        }
        return perks
    }
}
