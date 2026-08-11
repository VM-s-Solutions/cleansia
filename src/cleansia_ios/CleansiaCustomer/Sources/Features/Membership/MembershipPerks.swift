import Foundation

/// What a customer surface may say about the express surcharge waiver.
///
/// `expressUpgradesRemaining` is `0` for a member still inside the trial **and** for one who has used
/// the month's allowance up, so `trialEndsAtUtc` is the only field that separates them — a trialing
/// member is active and keeps the discount and the cancellation window, but earns no waiver, and
/// telling them they used theirs up would be a fresh false claim.
enum ExpressWaiverStatus: Equatable {
    case none
    case trial
    case available
    case exhausted

    var isAdvertised: Bool {
        self != .none
    }

    static func resolve(
        hasMembership: Bool,
        upgradesPerMonth: Int?,
        upgradesRemaining: Int?,
        trialEndsAtUtc: Date?,
        now: Date
    ) -> ExpressWaiverStatus {
        // `upgradesPerMonth` null IS the non-member state by the server's own definition, so folding
        // it onto zero here lands on exactly the answer it means.
        guard hasMembership, (upgradesPerMonth ?? 0) > 0 else { return .none }
        // `upgradesRemaining` is not the same: null means *no membership* and zero means *used up or
        // trialing*, so collapsing the two tells a member on a quota plan they spent a benefit they
        // paid for. Nothing here can tell which it is, and silence is the only answer that is not a
        // claim.
        guard let upgradesRemaining else { return .none }
        if let trialEndsAtUtc, trialEndsAtUtc > now { return .trial }
        return upgradesRemaining > 0 ? .available : .exhausted
    }

    static func resolve(_ membership: MyMembership?, now: Date = Date()) -> ExpressWaiverStatus {
        guard let membership else { return .none }
        return resolve(
            hasMembership: membership.hasMembership,
            upgradesPerMonth: membership.expressUpgradesPerMonth,
            upgradesRemaining: membership.expressUpgradesRemaining,
            trialEndsAtUtc: membership.trialEndsAtUtc,
            now: now
        )
    }

    static func resolve(_ snapshot: MembershipSnapshot?, now: Date = Date()) -> ExpressWaiverStatus {
        guard let snapshot else { return .none }
        return resolve(
            hasMembership: snapshot.hasMembership,
            upgradesPerMonth: snapshot.expressUpgradesPerMonth,
            upgradesRemaining: snapshot.expressUpgradesRemaining,
            trialEndsAtUtc: snapshot.trialEndsAtUtc,
            now: now
        )
    }
}

/// The three express states a perk row can actually render, so the label switch stays total —
/// `ExpressWaiverStatus.none` is an absent perk, not a perk with nothing to say.
enum MembershipExpressPerk: Equatable {
    case available(remaining: Int)
    case exhausted
    case pendingTrial

    init?(status: ExpressWaiverStatus, remaining: Int) {
        switch status {
        case .none: return nil
        case .available: self = .available(remaining: remaining)
        case .exhausted: self = .exhausted
        case .trial: self = .pendingTrial
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
            case .pendingTrial: L10n.Membership.perkPillExpressTrial
            }
        }
    }
}

enum MembershipPerks {
    static func resolve(_ membership: MyMembership, now: Date = Date()) -> [MembershipPerk] {
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
        if let express = MembershipExpressPerk(
            status: ExpressWaiverStatus.resolve(membership, now: now),
            remaining: membership.expressUpgradesRemaining ?? 0
        ) {
            perks.append(.express(express))
        }
        return perks
    }
}
