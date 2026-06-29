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
        let plusFreeHours = rawPlusHours.flatMap { $0 > standardFreeHours ? $0 : nil }
        let freeHours = plusFreeHours ?? standardFreeHours
        return CancellationPolicy(
            freeHours: freeHours,
            penaltyHours: penaltyHours,
            plusFreeHours: plusFreeHours,
            showMidTier: freeHours > penaltyHours
        )
    }
}
