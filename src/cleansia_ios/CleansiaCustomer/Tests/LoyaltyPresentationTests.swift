import XCTest
@testable import CleansiaCustomer

final class LoyaltyPresentationTests: XCTestCase {
    func testTierFromValueMapsKnownAndUnknown() {
        XCTAssertEqual(LoyaltyTier(value: 1), .bronzeCleaner)
        XCTAssertEqual(LoyaltyTier(value: 4), .platinumSparkler)
        XCTAssertNil(LoyaltyTier(value: 9))
        XCTAssertNil(LoyaltyTier(value: nil))
    }

    func testProgressFractionFromLifetimeAndNextThreshold() throws {
        let account = LoyaltyFixtures.account(lifetimePoints: 600, pointsToNextTier: 400)
        let progress = try XCTUnwrap(LoyaltyPresentation.progressFraction(account))
        XCTAssertEqual(progress, 0.6, accuracy: 0.0001)
    }

    func testProgressNextThresholdIsLifetimePlusPointsToNext() {
        let account = LoyaltyFixtures.account(lifetimePoints: 600, pointsToNextTier: 400)
        XCTAssertEqual(LoyaltyPresentation.nextThreshold(account), 1000)
    }

    func testProgressNilWhenMaxTier() {
        let account = LoyaltyFixtures.account(pointsToNextTier: nil, nextTier: nil)
        XCTAssertNil(LoyaltyPresentation.nextThreshold(account))
        XCTAssertNil(LoyaltyPresentation.progressFraction(account))
    }

    func testDiscountSummaryBranches() {
        let none = LoyaltyFixtures.tier(1, threshold: 0, discount: 0)
        XCTAssertEqual(LoyaltyPresentation.discountSummary(none), .noDiscount)

        let basic = LoyaltyFixtures.tier(3, threshold: 1000, discount: 0.10)
        XCTAssertEqual(LoyaltyPresentation.discountSummary(basic), .basic(percent: 10))

        let minOrder = LoyaltyFixtures.tier(2, threshold: 500, discount: 0.05, minOrder: 1000)
        XCTAssertEqual(LoyaltyPresentation.discountSummary(minOrder), .minOrder(percent: 5, minOrder: 1000))
    }

    func testTierStatusRelativeToCurrent() {
        XCTAssertEqual(LoyaltyPresentation.status(for: .silverMopper, current: .silverMopper), .current)
        XCTAssertEqual(LoyaltyPresentation.status(for: .bronzeCleaner, current: .silverMopper), .unlocked)
        XCTAssertEqual(LoyaltyPresentation.status(for: .goldPolisher, current: .silverMopper), .locked)
    }

    func testEffectivePerksFallBackToWelcomeBadgeWhenEmpty() {
        let effective = LoyaltyPresentation.effectivePerks([])
        XCTAssertEqual(effective.count, 1)
        XCTAssertEqual(effective.first?.labelKey, "loyalty.perks.welcome_badge")

        let supplied = LoyaltyPresentation.effectivePerks([TierPerk(
            icon: "x",
            labelKey: "loyalty.perks.dedicated_pool"
        )])
        XCTAssertEqual(supplied.count, 1)
        XCTAssertEqual(supplied.first?.labelKey, "loyalty.perks.dedicated_pool")
    }

    func testTransactionDescriptionPerSource() {
        let earn = LoyaltyFixtures.activityItem(points: 100, source: 1, orderNumber: "1042")
        XCTAssertEqual(LoyaltyPresentation.transactionKind(earn), .earnOrder(points: 100, order: "1042"))

        let revoke = LoyaltyFixtures.activityItem(points: -50, source: 2, orderNumber: "1042")
        XCTAssertEqual(LoyaltyPresentation.transactionKind(revoke), .revokeOrder(points: -50, order: "1042"))

        let referral = LoyaltyFixtures.activityItem(points: 150, source: 3, orderNumber: nil)
        XCTAssertEqual(LoyaltyPresentation.transactionKind(referral), .referral(points: 150))

        let manual = LoyaltyFixtures.activityItem(points: 25, source: 4, orderNumber: nil)
        XCTAssertEqual(LoyaltyPresentation.transactionKind(manual), .manual(points: 25))
    }

    func testTransactionOrderRefFallsBackToDash() {
        let noRef = LoyaltyFixtures.activityItem(points: 100, source: 1, orderNumber: nil)
        XCTAssertEqual(LoyaltyPresentation.transactionKind(noRef), .earnOrder(points: 100, order: "—"))
    }

    func testReferralStatsVariantByCounters() {
        XCTAssertEqual(LoyaltyPresentation.referralStats(accepted: 0, qualified: 0), .empty)
        XCTAssertEqual(LoyaltyPresentation.referralStats(accepted: 3, qualified: 0), .waiting(accepted: 3))
        XCTAssertEqual(
            LoyaltyPresentation.referralStats(accepted: 3, qualified: 2),
            .qualified(accepted: 3, qualified: 2)
        )
    }
}

final class RewardsShareTests: XCTestCase {
    func testShareTextEmbedsCodeAndLandingUrl() {
        let text = RewardsShare.message(code: "ABC123")
        XCTAssertTrue(text.contains("ABC123"))
        XCTAssertTrue(text.contains("https://cleansia.cz/r/ABC123"))
    }
}
