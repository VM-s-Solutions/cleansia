import XCTest
@testable import CleansiaCustomer

final class MembershipPerksTests: XCTestCase {
    func testInactiveMembershipCarriesNoPerks() {
        XCTAssertEqual(MembershipPerks.resolve(MembershipFixtures.inactive), [])
    }

    func testActiveMembershipCarriesDiscountCancellationAndRecurring() {
        XCTAssertEqual(
            MembershipPerks.resolve(MembershipFixtures.active),
            [.discount(percent: 5), .freeCancellation(hours: 4), .recurring]
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

    /// The express-upgrade perk is advertised and returned by the API but no pricing code enforces it,
    /// so it must not be rendered — a member pays the same express surcharge as everyone else.
    func testExpressUpgradeIsNeverRendered() {
        let perks = MembershipPerks.resolve(MembershipFixtures.active)
        XCTAssertTrue(MembershipFixtures.active.allowsExpressUpgrade == true)
        XCTAssertEqual(perks.filter { $0.systemImage == "bolt" }, [])
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
            billingInterval: membership.billingInterval
        )
        XCTAssertEqual(
            MembershipPerks.resolve(membership),
            [.discount(percent: 5), .freeCancellation(hours: 4), .recurring]
        )
    }

    func testEveryPerkResolvesALocalizedLabel() {
        for perk in MembershipPerks.resolve(MembershipFixtures.active) {
            XCTAssertFalse(perk.label.isEmpty)
            XCTAssertFalse(perk.label.hasPrefix("membership_perk_pill_"), "\(perk) fell through to its key")
        }
    }

    private func membership(discount: Double?, cancellationHours: Int?) -> MyMembership {
        MyMembership(
            hasMembership: true,
            planCode: "plus_monthly",
            planName: "Cleansia Plus",
            discountPercentage: discount,
            freeCancellationWindowHours: cancellationHours,
            allowsExpressUpgrade: true,
            currentPeriodEnd: nil,
            cancelRequested: false,
            billingInterval: 1
        )
    }
}
