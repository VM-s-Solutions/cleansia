import XCTest
@testable import CleansiaCustomer

final class BookingPricingTests: XCTestCase {
    private let now = Date(timeIntervalSince1970: 1_700_000_000)

    private func lead(hours: Double) -> Date {
        now.addingTimeInterval(hours * 3600)
    }

    func testNoSurchargeWhenCleaningAtIsNil() {
        XCTAssertFalse(BookingPricing.requiresExpressSurcharge(cleaningAt: nil, now: now))
        XCTAssertEqual(BookingPricing.expressSurchargeAmount(basePrice: 1000, cleaningAt: nil, now: now), 0)
    }

    func testExpressBandLowerBoundInclusive() {
        XCTAssertTrue(BookingPricing.requiresExpressSurcharge(cleaningAt: lead(hours: 2.0), now: now))
    }

    func testExpressBandJustBelowStandardIsSurcharged() {
        XCTAssertTrue(BookingPricing.requiresExpressSurcharge(cleaningAt: lead(hours: 3.99), now: now))
    }

    func testStandardLeadIsNotSurcharged() {
        XCTAssertFalse(BookingPricing.requiresExpressSurcharge(cleaningAt: lead(hours: 4.0), now: now))
        XCTAssertFalse(BookingPricing.requiresExpressSurcharge(cleaningAt: lead(hours: 5.0), now: now))
    }

    func testBelowExpressBandIsNotSurchargedClientSide() {
        XCTAssertFalse(BookingPricing.requiresExpressSurcharge(cleaningAt: lead(hours: 1.0), now: now))
    }

    func testSurchargeAmountIsTwentyPercentInBand() {
        XCTAssertEqual(
            BookingPricing.expressSurchargeAmount(basePrice: 1000, cleaningAt: lead(hours: 3.0), now: now),
            200,
            accuracy: 0.0001
        )
    }

    func testSimpleFinalTotalAddsSurchargeOnBase() {
        XCTAssertEqual(
            BookingPricing.finalTotal(basePrice: 1000, cleaningAt: lead(hours: 3.0), now: now),
            1200,
            accuracy: 0.0001
        )
        XCTAssertEqual(
            BookingPricing.finalTotal(basePrice: 1000, cleaningAt: lead(hours: 5.0), now: now),
            1000,
            accuracy: 0.0001
        )
    }

    func testDiscountAppliesBeforeSurchargeAndUsesMaxOfTierPromo() {
        let total = BookingPricing.finalTotal(
            basePrice: 1000,
            cleaningAt: lead(hours: 3.0),
            tierDiscount: 100,
            promoDiscount: 300,
            now: now
        )
        XCTAssertEqual(total, 840, accuracy: 0.0001)
    }

    func testDiscountWithoutSurchargeOutsideBand() {
        let total = BookingPricing.finalTotal(
            basePrice: 1000,
            cleaningAt: lead(hours: 6.0),
            tierDiscount: 250,
            promoDiscount: 100,
            now: now
        )
        XCTAssertEqual(total, 750, accuracy: 0.0001)
    }

    func testDiscountNeverDrivesBaseNegative() {
        let total = BookingPricing.finalTotal(
            basePrice: 100,
            cleaningAt: nil,
            tierDiscount: 0,
            promoDiscount: 500,
            now: now
        )
        XCTAssertEqual(total, 0, accuracy: 0.0001)
    }

    func testTierWinsWhenLargerThanPromo() {
        let total = BookingPricing.finalTotal(
            basePrice: 1000,
            cleaningAt: nil,
            tierDiscount: 400,
            promoDiscount: 100,
            now: now
        )
        XCTAssertEqual(total, 600, accuracy: 0.0001)
    }

    func testCurrencySymbolMapping() {
        XCTAssertEqual(BookingPricing.currencySymbol(for: "CZK"), "Kč")
        XCTAssertEqual(BookingPricing.currencySymbol(for: "eur"), "€")
        XCTAssertEqual(BookingPricing.currencySymbol(for: "USD"), "$")
        XCTAssertEqual(BookingPricing.currencySymbol(for: "GBP"), "GBP")
    }

    func testFormatTotalRoundsToWholeWithSymbol() {
        XCTAssertEqual(BookingPricing.formatTotal(1200.4, currencyCode: "CZK"), "1200 Kč")
    }
}
