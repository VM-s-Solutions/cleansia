import XCTest
@testable import CleansiaCustomer

final class MembershipPerksTests: XCTestCase {
    private static let now = Date(timeIntervalSince1970: 1_780_000_000)

    func testInactiveMembershipCarriesNoPerks() {
        XCTAssertEqual(MembershipPerks.resolve(MembershipFixtures.inactive), [])
    }

    /// Every benefit stops on the first failed renewal, so a past-due member is shown none of them.
    func testAPastDueMembershipCarriesNoPerksAndNoExpressWaiver() {
        XCTAssertEqual(MembershipPerks.resolve(MembershipFixtures.pastDue), [])
        XCTAssertEqual(ExpressWaiverStatus.resolve(MembershipFixtures.pastDue), .none)
        let snapshot = MembershipSnapshot(
            hasMembership: true,
            freeCancellationWindowHours: 4,
            expressUpgradesPerMonth: 2,
            expressUpgradesRemaining: 1,
            benefitsPaused: true
        )
        XCTAssertEqual(ExpressWaiverStatus.resolve(snapshot), .none)
    }

    func testTheSubscribeTilesStateThePlansOwnFigures() {
        let monthly = MembershipFixtures.plans[0]
        XCTAssertEqual(monthly.discountPerkPercent, 5)
        XCTAssertEqual(monthly.cancellationPerkHours, 4)
        XCTAssertEqual(monthly.expressPerkPerMonth, 2)

        let bare = plan(discount: 0, cancellationHours: 24, allowsExpress: true, expressPerMonth: 0)
        XCTAssertNil(bare.discountPerkPercent)
        XCTAssertNil(bare.cancellationPerkHours, "a window no closer than the standard one is no perk")
        XCTAssertNil(bare.expressPerkPerMonth)

        let noExpress = plan(discount: 7.9, cancellationHours: 6, allowsExpress: false, expressPerMonth: 3)
        XCTAssertEqual(noExpress.discountPerkPercent, 7)
        XCTAssertEqual(noExpress.cancellationPerkHours, 6)
        XCTAssertNil(noExpress.expressPerkPerMonth, "a quota the plan does not honour is not advertised")
    }

    func testEveryDynamicPerkSentenceCarriesItsFigure() {
        XCTAssertTrue(L10n.Membership.perkDiscountDesc(7).contains("7"))
        XCTAssertTrue(L10n.Membership.perkCancellationDesc(6).contains("6"))
        XCTAssertTrue(L10n.Membership.perkExpressDesc(3).contains("3"))
    }

    private func plan(
        discount: Double,
        cancellationHours: Int,
        allowsExpress: Bool,
        expressPerMonth: Int
    ) -> MembershipPlan {
        MembershipPlan(
            code: "plus_monthly",
            name: "Monthly",
            price: 199,
            monthlyEquivalentPrice: 199,
            billingInterval: 1,
            discountPercentage: discount,
            freeCancellationWindowHours: cancellationHours,
            allowsExpressUpgrade: allowsExpress,
            expressUpgradesPerMonth: expressPerMonth,
            trialPeriodDays: 0,
            savingsPercentVsMonthly: 0,
            currencyCode: "CZK"
        )
    }

    func testActiveMembershipCarriesDiscountCancellationRecurringAndExpress() {
        XCTAssertEqual(
            MembershipPerks.resolve(MembershipFixtures.active),
            [.discount(percent: 5), .freeCancellation(hours: 4), .recurring, .express(.available(remaining: 1))]
        )
    }

    func testZeroDiscountIsOmitted() {
        let perks = MembershipPerks.resolve(membership(discount: 0, cancellationHours: 4))
        XCTAssertEqual(perks, [.freeCancellation(hours: 4), .recurring])
    }

    func testMissingDiscountIsOmitted() {
        let perks = MembershipPerks.resolve(membership(discount: nil, cancellationHours: 4))
        XCTAssertEqual(perks, [.freeCancellation(hours: 4), .recurring])
    }

    func testZeroCancellationWindowIsOmitted() {
        let perks = MembershipPerks.resolve(membership(discount: 5, cancellationHours: 0))
        XCTAssertEqual(perks, [.discount(percent: 5), .recurring])
    }

    func testMissingCancellationWindowIsOmitted() {
        let perks = MembershipPerks.resolve(membership(discount: 5, cancellationHours: nil))
        XCTAssertEqual(perks, [.discount(percent: 5), .recurring])
    }

    func testFractionalDiscountTruncatesToWholePercent() {
        let perks = MembershipPerks.resolve(membership(discount: 7.9, cancellationHours: nil))
        XCTAssertEqual(perks, [.discount(percent: 7), .recurring])
    }

    /// A plan without the express quota advertises no express perk at all — the row is the plan's, not
    /// the layout's.
    func testAPlanWithNoExpressQuotaAdvertisesNoExpressPerk() {
        let perks = MembershipPerks.resolve(
            membership(discount: 5, cancellationHours: 4, perMonth: 0, remaining: 0)
        )
        XCTAssertEqual(perks, [.discount(percent: 5), .freeCancellation(hours: 4), .recurring])
    }

    func testAnExhaustedQuotaStillAdvertisesThePerkAsUsedUp() {
        let perks = MembershipPerks.resolve(
            membership(discount: 5, cancellationHours: 4, perMonth: 2, remaining: 0)
        )
        XCTAssertEqual(perks.last, .express(.exhausted))
    }

    /// A member inside the free trial holds every Plus benefit from day one, the express waiver included.
    func testATrialingMemberHoldsEveryPerkAPayingMemberDoes() {
        let perks = MembershipPerks.resolve(
            membership(
                discount: 5,
                cancellationHours: 4,
                perMonth: 2,
                remaining: 2,
                trialEndsAtUtc: Self.now.addingTimeInterval(3600)
            )
        )
        XCTAssertEqual(
            perks,
            [.discount(percent: 5), .freeCancellation(hours: 4), .recurring, .express(.available(remaining: 2))]
        )
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
            MembershipPerks.resolve(membership),
            [.discount(percent: 5), .freeCancellation(hours: 4), .recurring, .express(.available(remaining: 1))]
        )
    }

    func testEveryPerkResolvesALocalizedLabel() {
        for perk in MembershipPerks.resolve(MembershipFixtures.active) {
            XCTAssertFalse(perk.label.isEmpty)
            XCTAssertFalse(perk.label.hasPrefix("membership_perk_pill_"), "\(perk) fell through to its key")
        }
    }

    func testTheExpressLabelsAreDistinctSoNoStateReadsAsAnother() {
        XCTAssertNotEqual(
            MembershipPerk.express(.available(remaining: 1)).label,
            MembershipPerk.express(.exhausted).label
        )
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
        XCTAssertEqual(ExpressWaiverStatus.resolve(nil as MyMembership?), .none)
        XCTAssertEqual(ExpressWaiverStatus.resolve(MembershipFixtures.inactive), .none)
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

    /// The server states the shapes in as many words: *null = no membership; 0 = exhausted*. Collapsing
    /// null onto zero therefore tells a member on a quota plan that they used up a benefit they paid for —
    /// the coerced value is the opposite of what the server's own null means, and "used up" is a claim
    /// where silence is not.
    func testAnUnreportedQuotaIsNeverReportedAsUsedUp() {
        XCTAssertEqual(status(perMonth: 2, remaining: nil), .none)
        XCTAssertNotEqual(status(perMonth: 2, remaining: nil), .exhausted)
    }

    func testAnUnreportedQuotaIsNotAdvertised() {
        XCTAssertFalse(status(perMonth: 2, remaining: nil).isAdvertised)
    }

    /// A member inside the trial is entitled like a paying one, so the server's count is read as it is.
    func testATrialInFlightReadsTheServersCount() {
        let trialEnd = Self.now.addingTimeInterval(86400)
        XCTAssertEqual(status(perMonth: 2, remaining: 2, trialEndsAtUtc: trialEnd), .available)
        XCTAssertEqual(status(perMonth: 2, remaining: 0, trialEndsAtUtc: trialEnd), .exhausted)
    }

    func testOnlyTheNoneCaseIsUnadvertised() {
        XCTAssertFalse(ExpressWaiverStatus.none.isAdvertised)
        XCTAssertTrue(ExpressWaiverStatus.available.isAdvertised)
        XCTAssertTrue(ExpressWaiverStatus.exhausted.isAdvertised)
    }

    func testTheBookingSnapshotResolvesIdenticallyToTheMembershipRecord() {
        let snapshot = MembershipSnapshot(
            hasMembership: true,
            freeCancellationWindowHours: 48,
            expressUpgradesPerMonth: 2,
            expressUpgradesRemaining: 0
        )
        XCTAssertEqual(ExpressWaiverStatus.resolve(snapshot), .exhausted)
        XCTAssertEqual(ExpressWaiverStatus.resolve(nil as MembershipSnapshot?), .none)
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
            )
        )
    }
}
