import XCTest
@testable import CleansiaCustomer

final class BookingPricingTests: XCTestCase {
    private let now = Date(timeIntervalSince1970: 1_700_000_000)

    private func lead(hours: Double) -> Date {
        now.addingTimeInterval(hours * 3600)
    }

    func testNoSurchargeWhenCleaningAtIsNil() {
        XCTAssertFalse(BookingPricing.requiresExpressSurcharge(cleaningAt: nil, now: now))
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

/// The number on screen has to be the number charged. `QuoteOrderResponse.totalPrice` already folds the
/// express surcharge in, so every row here is the server's arithmetic read back, never re-derived.
final class BookingPriceSummaryTests: XCTestCase {
    private func quote(
        total: Double,
        surcharge: Double = 0,
        applied: Bool = false,
        waived: Bool = false
    ) -> BookingQuote {
        BookingQuote(
            totalPrice: total,
            currencyCode: "CZK",
            expressSurchargeApplied: applied,
            expressSurchargeAmount: surcharge,
            expressSurchargeWaivedByMembership: waived
        )
    }

    func testNoQuoteYieldsAnEmptySummary() {
        let summary = BookingPriceSummary.resolve(quote: nil, discount: 250)
        XCTAssertEqual(summary, BookingPriceSummary(
            subtotal: 0,
            expressSurcharge: 0,
            expressLine: .notExpress,
            total: 0
        ))
    }

    func testAStandardSlotShowsNoExpressRow() {
        let summary = BookingPriceSummary.resolve(quote: quote(total: 1000), discount: 0)
        XCTAssertEqual(summary.expressLine, .notExpress)
        XCTAssertEqual(summary.subtotal, 1000, accuracy: 0.0001)
        XCTAssertEqual(summary.total, 1000, accuracy: 0.0001)
    }

    /// The subtotal row is the pre-surcharge figure and the total is the server's — added together they
    /// must not double-count the surcharge the server already applied.
    func testAChargedExpressSlotSplitsTheServerTotalWithoutReapplyingTheRate() {
        let summary = BookingPriceSummary.resolve(
            quote: quote(total: 1200, surcharge: 200, applied: true),
            discount: 0
        )
        XCTAssertEqual(summary.expressLine, .charged)
        XCTAssertEqual(summary.subtotal, 1000, accuracy: 0.0001)
        XCTAssertEqual(summary.expressSurcharge, 200, accuracy: 0.0001)
        XCTAssertEqual(summary.total, 1200, accuracy: 0.0001)
    }

    /// `expressSurchargeApplied == false` is equally true for a slot that is not express at all, so the
    /// waived row rides its own server field.
    func testAWaivedExpressSlotShowsTheWaiverAndChargesNothingForIt() {
        let summary = BookingPriceSummary.resolve(
            quote: quote(total: 1000, surcharge: 0, applied: false, waived: true),
            discount: 0
        )
        XCTAssertEqual(summary.expressLine, .waived)
        XCTAssertEqual(summary.expressSurcharge, 0, accuracy: 0.0001)
        XCTAssertEqual(summary.subtotal, 1000, accuracy: 0.0001)
        XCTAssertEqual(summary.total, 1000, accuracy: 0.0001)
    }

    func testTheWaivedVerdictWinsOverTheChargedOne() {
        let summary = BookingPriceSummary.resolve(
            quote: quote(total: 1000, surcharge: 0, applied: true, waived: true),
            discount: 0
        )
        XCTAssertEqual(summary.expressLine, .waived)
    }

    func testTheDiscountComesOffTheServerTotal() {
        let summary = BookingPriceSummary.resolve(
            quote: quote(total: 1200, surcharge: 200, applied: true),
            discount: 300
        )
        XCTAssertEqual(summary.total, 900, accuracy: 0.0001)
    }

    func testADiscountNeverDrivesTheTotalNegative() {
        let summary = BookingPriceSummary.resolve(quote: quote(total: 100), discount: 500)
        XCTAssertEqual(summary.total, 0, accuracy: 0.0001)
    }

    /// `CreateOrder.Handler`'s own base: `calc.TotalPrice - calc.ExpressSurchargeAmount`.
    func testThePreSurchargeSubtotalIsTheGrossLessTheServersOwnSurchargeAmount() {
        XCTAssertEqual(
            quote(total: 1200, surcharge: 200, applied: true).preSurchargeSubtotal,
            1000,
            accuracy: 0.0001
        )
    }

    func testWithoutASurchargeThePreSurchargeSubtotalIsTheWholeQuote() {
        XCTAssertEqual(quote(total: 1000).preSurchargeSubtotal, 1000, accuracy: 0.0001)
    }

    /// The surcharge here is deliberately not 20 % of anything: only the quote's own ratio takes 100 to
    /// 115, so a restatement hardcoding the express rate fails this and a passing one owns no rate.
    func testRestatingADiscountUsesTheQuotesOwnRatioNeverTheExpressRate() {
        XCTAssertEqual(
            quote(total: 1150, surcharge: 150, applied: true).discountAsCharged(100),
            115,
            accuracy: 0.0001
        )
    }

    /// The customer would have paid 1200 and pays (1000 - 100) * 1.2 = 1080, so they saved 120.
    func testADiscountResolvedOnTheExpressBaseIsRestatedAgainstTheChargedPrice() {
        let quoted = quote(total: 1200, surcharge: 200, applied: true)

        XCTAssertEqual(quoted.discountAsCharged(100), 120, accuracy: 0.0001)
        XCTAssertEqual(
            BookingPriceSummary.resolve(quote: quoted, discount: quoted.discountAsCharged(100)).total,
            1080,
            accuracy: 0.0001
        )
    }

    func testWithoutASurchargeADiscountIsAlreadyStatedAgainstTheChargedPrice() {
        XCTAssertEqual(quote(total: 1000).discountAsCharged(100), 100, accuracy: 0.0001)
    }

    /// A waived slot is charged no surcharge, so it is the plain case however express the hour is.
    func testAWaivedExpressSlotLeavesTheDiscountAlone() {
        XCTAssertEqual(
            quote(total: 1000, surcharge: 0, applied: true, waived: true).discountAsCharged(100),
            100,
            accuracy: 0.0001
        )
    }

    func testAnEmptyBaseCannotScaleAnythingAndReturnsTheDiscountUnchanged() {
        XCTAssertEqual(quote(total: 0).discountAsCharged(50), 50, accuracy: 0.0001)
    }
}
