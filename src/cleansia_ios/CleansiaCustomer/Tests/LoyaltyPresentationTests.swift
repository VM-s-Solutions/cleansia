import CleansiaCore
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

    /// Where no floor applies the tier still discounts — the line loses its floor, not its percent.
    func testAFloorThatDoesNotApplyReadsAsABasicDiscount() {
        let minOrder = LoyaltyFixtures.tier(2, threshold: 500, discount: 0.05, minOrder: 1000)
        XCTAssertEqual(LoyaltyPresentation.discountSummary(minOrder, floorApplies: false), .basic(percent: 5))
    }

    func testTheFloorAppliesOnlyInAMarketOnTheDefaultCurrency() {
        let resolvedOnDefault = MarketState.resolved(selected: MarketFixtures.czechia, markets: MarketFixtures.two)
        XCTAssertEqual(
            LoyaltyPresentation.tierFloor(market: resolvedOnDefault, catalogDefaultCurrencyCode: "EUR"),
            .applies(currencyCode: "CZK"),
            "the market's own code labels it, not the catalogue's"
        )

        let resolvedElsewhere = MarketState.resolved(selected: MarketFixtures.slovakia, markets: MarketFixtures.two)
        XCTAssertEqual(
            LoyaltyPresentation.tierFloor(market: resolvedElsewhere, catalogDefaultCurrencyCode: "CZK"),
            .notApplicable
        )

        let germany = MarketFixtures.market(
            countryId: "deu", isoCode: "DEU", isoAlpha2: "DE", name: "Germany", currencyCode: "EUR", isDefault: true
        )
        XCTAssertEqual(
            LoyaltyPresentation.tierFloor(
                market: .resolved(selected: MarketFixtures.slovakia, markets: [germany, MarketFixtures.slovakia]),
                catalogDefaultCurrencyCode: "EUR"
            ),
            .applies(currencyCode: "EUR"),
            "two markets sharing the default currency both state the floor"
        )

        let noneFlagged = MarketState.resolved(selected: MarketFixtures.slovakia, markets: [MarketFixtures.slovakia])
        XCTAssertEqual(
            LoyaltyPresentation.tierFloor(market: noneFlagged, catalogDefaultCurrencyCode: "CZK"),
            .notApplicable
        )

        XCTAssertEqual(
            LoyaltyPresentation.tierFloor(market: .unavailable, catalogDefaultCurrencyCode: "CZK"),
            .applies(currencyCode: "CZK")
        )
        XCTAssertEqual(
            LoyaltyPresentation.tierFloor(market: .loading, catalogDefaultCurrencyCode: nil),
            .applies(currencyCode: nil)
        )
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
            icon: "percent",
            labelKey: "loyalty.perks.discount_5_above_1000"
        )])
        XCTAssertEqual(supplied.count, 1)
        XCTAssertEqual(supplied.first?.labelKey, "loyalty.perks.discount_5_above_1000")
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
        XCTAssertTrue(text.contains(CleansiaWeb.referralLink(code: "ABC123")))
    }
}
