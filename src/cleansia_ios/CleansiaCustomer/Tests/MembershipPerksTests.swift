import XCTest
@testable import CleansiaCustomer

final class MembershipPerksTests: XCTestCase {
    private static let now = Date(timeIntervalSince1970: 1_780_000_000)

    func testInactiveMembershipCarriesNoPerks() {
        XCTAssertEqual(MembershipPerks.resolve(MembershipFixtures.inactive), [])
    }

    func testActiveMembershipCarriesDiscountCancellationRecurringAndExpress() {
        XCTAssertEqual(
            MembershipPerks.resolve(MembershipFixtures.active, now: Self.now),
            [.discount(percent: 5), .freeCancellation(hours: 4), .recurring, .express(.available(remaining: 1))]
        )
    }

    func testZeroDiscountIsOmitted() {
        let perks = MembershipPerks.resolve(membership(discount: 0, cancellationHours: 4), now: Self.now)
        XCTAssertEqual(perks, [.freeCancellation(hours: 4), .recurring])
    }

    func testMissingDiscountIsOmitted() {
        let perks = MembershipPerks.resolve(membership(discount: nil, cancellationHours: 4), now: Self.now)
        XCTAssertEqual(perks, [.freeCancellation(hours: 4), .recurring])
    }

    func testZeroCancellationWindowIsOmitted() {
        let perks = MembershipPerks.resolve(membership(discount: 5, cancellationHours: 0), now: Self.now)
        XCTAssertEqual(perks, [.discount(percent: 5), .recurring])
    }

    func testMissingCancellationWindowIsOmitted() {
        let perks = MembershipPerks.resolve(membership(discount: 5, cancellationHours: nil), now: Self.now)
        XCTAssertEqual(perks, [.discount(percent: 5), .recurring])
    }

    func testFractionalDiscountTruncatesToWholePercent() {
        let perks = MembershipPerks.resolve(membership(discount: 7.9, cancellationHours: nil), now: Self.now)
        XCTAssertEqual(perks, [.discount(percent: 7), .recurring])
    }

    /// A plan without the express quota advertises no express perk at all — the row is the plan's, not
    /// the layout's.
    func testAPlanWithNoExpressQuotaAdvertisesNoExpressPerk() {
        let perks = MembershipPerks.resolve(
            membership(discount: 5, cancellationHours: 4, perMonth: 0, remaining: 0),
            now: Self.now
        )
        XCTAssertEqual(perks, [.discount(percent: 5), .freeCancellation(hours: 4), .recurring])
    }

    func testAnExhaustedQuotaStillAdvertisesThePerkAsUsedUp() {
        let perks = MembershipPerks.resolve(
            membership(discount: 5, cancellationHours: 4, perMonth: 2, remaining: 0),
            now: Self.now
        )
        XCTAssertEqual(perks.last, .express(.exhausted))
    }

    /// The owner ruled no express waivers during the trial. A trialing member's remaining count is 0 for
    /// the same reason an exhausted member's is, so reporting "used up" would be a fresh false claim.
    func testATrialingMemberIsToldTheWaiverHasNotStartedYet() {
        let perks = MembershipPerks.resolve(
            membership(
                discount: 5,
                cancellationHours: 4,
                perMonth: 2,
                remaining: 0,
                trialEndsAtUtc: Self.now.addingTimeInterval(3600)
            ),
            now: Self.now
        )
        XCTAssertEqual(perks.last, .express(.pendingTrial))
    }

    func testATrialingMemberKeepsTheDiscountAndTheCancellationWindow() {
        let perks = MembershipPerks.resolve(
            membership(
                discount: 5,
                cancellationHours: 4,
                perMonth: 2,
                remaining: 0,
                trialEndsAtUtc: Self.now.addingTimeInterval(3600)
            ),
            now: Self.now
        )
        XCTAssertTrue(perks.contains(.discount(percent: 5)))
        XCTAssertTrue(perks.contains(.freeCancellation(hours: 4)))
    }

    func testAnExpiredTrialEarnsTheWaiverAgain() {
        let perks = MembershipPerks.resolve(
            membership(
                discount: 5,
                cancellationHours: 4,
                perMonth: 2,
                remaining: 2,
                trialEndsAtUtc: Self.now.addingTimeInterval(-3600)
            ),
            now: Self.now
        )
        XCTAssertEqual(perks.last, .express(.available(remaining: 2)))
    }

    func testRecurringSurvivesACancellationRequest() {
        var membership = MembershipFixtures.active
        membership = MyMembership(
            hasMembership: true,
            planCode: membership.planCode,
            planName: membership.planName,
            discountPercentage: membership.discountPercentage,
            freeCancellationWindowHours: membership.freeCancellationWindowHours,
            allowsExpressUpgrade: membership.allowsExpressUpgrade,
            currentPeriodEnd: membership.currentPeriodEnd,
            cancelRequested: true,
            billingInterval: membership.billingInterval,
            expressUpgradesPerMonth: membership.expressUpgradesPerMonth,
            expressUpgradesRemaining: membership.expressUpgradesRemaining
        )
        XCTAssertEqual(
            MembershipPerks.resolve(membership, now: Self.now),
            [.discount(percent: 5), .freeCancellation(hours: 4), .recurring, .express(.available(remaining: 1))]
        )
    }

    func testEveryPerkResolvesALocalizedLabel() {
        for perk in MembershipPerks.resolve(MembershipFixtures.active, now: Self.now) {
            XCTAssertFalse(perk.label.isEmpty)
            XCTAssertFalse(perk.label.hasPrefix("membership_perk_pill_"), "\(perk) fell through to its key")
        }
    }

    func testTheThreeExpressLabelsAreDistinctSoNoStateReadsAsAnother() {
        let labels = [
            MembershipPerk.express(.available(remaining: 1)).label,
            MembershipPerk.express(.exhausted).label,
            MembershipPerk.express(.pendingTrial).label
        ]
        XCTAssertEqual(Set(labels).count, 3)
    }

    private func membership(
        discount: Double?,
        cancellationHours: Int?,
        perMonth: Int? = nil,
        remaining: Int? = nil,
        trialEndsAtUtc: Date? = nil
    ) -> MyMembership {
        MyMembership(
            hasMembership: true,
            planCode: "plus_monthly",
            planName: "Cleansia Plus",
            discountPercentage: discount,
            freeCancellationWindowHours: cancellationHours,
            allowsExpressUpgrade: true,
            currentPeriodEnd: nil,
            cancelRequested: false,
            billingInterval: 1,
            expressUpgradesPerMonth: perMonth,
            expressUpgradesRemaining: remaining,
            trialEndsAtUtc: trialEndsAtUtc
        )
    }
}

/// The status is what every express surface branches on — the booking slot grid, the management pills
/// and the two membership screens — so it is pinned once, here, against each shape the resolver on the
/// server can produce.
final class ExpressWaiverStatusTests: XCTestCase {
    private static let now = Date(timeIntervalSince1970: 1_780_000_000)

    func testAGuestOrNonMemberIsToldNothing() {
        XCTAssertEqual(ExpressWaiverStatus.resolve(nil as MyMembership?, now: Self.now), .none)
        XCTAssertEqual(ExpressWaiverStatus.resolve(MembershipFixtures.inactive, now: Self.now), .none)
    }

    func testAMemberOnAPlanWithoutTheQuotaIsToldNothing() {
        XCTAssertEqual(status(perMonth: 0, remaining: 0), .none)
        XCTAssertEqual(status(perMonth: nil, remaining: nil), .none)
    }

    func testQuotaLeftIsAvailable() {
        XCTAssertEqual(status(perMonth: 2, remaining: 1), .available)
    }

    func testNoQuotaLeftIsExhausted() {
        XCTAssertEqual(status(perMonth: 2, remaining: 0), .exhausted)
    }

    /// A trial member is active and keeps the other benefits but earns no waiver; the quota still
    /// reports the plan's number so the client can say WHEN waivers start.
    func testATrialInFlightIsNeverAvailableEvenWithQuotaReported() {
        XCTAssertEqual(
            status(perMonth: 2, remaining: 2, trialEndsAtUtc: Self.now.addingTimeInterval(1)),
            .trial
        )
        XCTAssertEqual(
            status(perMonth: 2, remaining: 0, trialEndsAtUtc: Self.now.addingTimeInterval(86400)),
            .trial
        )
    }

    func testATrialThatHasAlreadyEndedNoLongerSuppressesTheWaiver() {
        XCTAssertEqual(
            status(perMonth: 2, remaining: 2, trialEndsAtUtc: Self.now.addingTimeInterval(-1)),
            .available
        )
    }

    func testOnlyTheNoneCaseIsUnadvertised() {
        XCTAssertFalse(ExpressWaiverStatus.none.isAdvertised)
        XCTAssertTrue(ExpressWaiverStatus.trial.isAdvertised)
        XCTAssertTrue(ExpressWaiverStatus.available.isAdvertised)
        XCTAssertTrue(ExpressWaiverStatus.exhausted.isAdvertised)
    }

    func testTheBookingSnapshotResolvesIdenticallyToTheMembershipRecord() {
        let snapshot = MembershipSnapshot(
            hasMembership: true,
            freeCancellationWindowHours: 48,
            expressUpgradesPerMonth: 2,
            expressUpgradesRemaining: 0,
            trialEndsAtUtc: Self.now.addingTimeInterval(3600)
        )
        XCTAssertEqual(ExpressWaiverStatus.resolve(snapshot, now: Self.now), .trial)
        XCTAssertEqual(ExpressWaiverStatus.resolve(nil as MembershipSnapshot?, now: Self.now), .none)
    }

    private func status(perMonth: Int?, remaining: Int?, trialEndsAtUtc: Date? = nil) -> ExpressWaiverStatus {
        ExpressWaiverStatus.resolve(
            MyMembership(
                hasMembership: true,
                planCode: "plus_monthly",
                planName: "Cleansia Plus",
                discountPercentage: 5,
                freeCancellationWindowHours: 4,
                allowsExpressUpgrade: true,
                currentPeriodEnd: nil,
                cancelRequested: false,
                billingInterval: 1,
                expressUpgradesPerMonth: perMonth,
                expressUpgradesRemaining: remaining,
                trialEndsAtUtc: trialEndsAtUtc
            ),
            now: Self.now
        )
    }
}
