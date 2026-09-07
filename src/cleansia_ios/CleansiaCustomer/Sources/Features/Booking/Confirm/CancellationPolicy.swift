import Foundation

struct CancellationPolicy: Equatable {
    let freeHours: Int
    let penaltyHours: Int
    let plusFreeHours: Int?
    let showMidTier: Bool

    var hasPlusPerk: Bool {
        plusFreeHours != nil
    }
}

enum CancellationPolicyBuilder {
    static let standardFreeHours = 24
    static let penaltyHours = 4

    static func make(membership: MembershipSnapshot?) -> CancellationPolicy {
        let rawPlusHours = membership
            .flatMap { $0.hasMembership ? $0.freeCancellationWindowHours : nil }
            .flatMap { $0 > 0 ? $0 : nil }
        // SMALLER is the perk, and the comparison used to read `>`.
        //
        // `BookingPolicy.ClassifyCancellation` is `free when hoursBeforeStart >= freeWindow`, so a
        // plan window of 4 means free cancellation right up to 4 h before, where a non-member pays
        // 25 % from 24 h. The benefit is the deadline moving CLOSER to the cleaning, which is a
        // smaller number — and the seeded plans carry 4. Reading it as "wider = bigger" meant the
        // plans were rejected as not-a-perk, the badge never appeared, and a paying member was told
        // they had to cancel 24 h ahead to cancel free.
        let plusFreeHours = rawPlusHours.flatMap { $0 < standardFreeHours ? $0 : nil }
        let freeHours = plusFreeHours ?? standardFreeHours
        return CancellationPolicy(
            freeHours: freeHours,
            penaltyHours: penaltyHours,
            plusFreeHours: plusFreeHours,
            showMidTier: freeHours > penaltyHours
        )
    }
}
