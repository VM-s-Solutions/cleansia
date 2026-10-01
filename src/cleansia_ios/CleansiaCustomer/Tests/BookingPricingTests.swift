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
        XCTAssertEqual(BookingPricing.formatTotal(1000, currencyCode: "EUR"), "1000 €")
    }

    /// Before the catalogue has loaded there is no currency to label with, and a bare figure is
    /// the honest rendering — never a guessed suffix.
    func testFormatTotalWithNoCurrencyRendersTheBareAmount() {
        XCTAssertEqual(BookingPricing.formatTotal(1200, currencyCode: ""), "1200")
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
            dirtiness: .normal,
            dirtinessSurcharge: 0,
            expressSurcharge: 0,
            expressLine: .notExpress,
            total: 0,
            creditApplied: 0,
            creditBalance: 0
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

    /// The surcharge sits inside the base the server prices from, so it is lifted out of the subtotal
    /// onto its own row: subtotal + dirtiness - discount + express adds up to the total on screen.
    func testTheDirtinessSurchargeIsItsOwnRowAndTheRowsAddUpToTheTotal() {
        let heavyExpress = BookingQuote(
            totalPrice: 1920,
            currencyCode: "CZK",
            expressSurchargeApplied: true,
            expressSurchargeAmount: 320,
            dirtiness: .heavy,
            dirtinessSurchargeAmount: 600
        )

        let summary = BookingPriceSummary.resolve(quote: heavyExpress, discount: 0)

        XCTAssertEqual(summary.dirtiness, .heavy)
        XCTAssertEqual(summary.dirtinessSurcharge, 600, accuracy: 0.0001)
        XCTAssertEqual(summary.subtotal, 1000, accuracy: 0.0001)
        XCTAssertEqual(
            summary.subtotal + summary.dirtinessSurcharge + summary.expressSurcharge,
            summary.total,
            accuracy: 0.0001
        )
        XCTAssertEqual(heavyExpress.preSurchargeSubtotal, 1600, accuracy: 0.0001, "the discount base keeps the level")
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

/// The confirm step's credit preview, held to `BookingPolicy.CapCreditForOrder` with the server's own
/// vectors (`CreditAtCheckoutTests`) and the ones the Android twin pins. The share is the server's; only
/// the min/floor formula lives here.
final class CreditCapTests: XCTestCase {
    private func cap(_ balance: Double, _ charged: Double, _ share: Double = 0.7) -> Double {
        BookingPricing.capCreditForOrder(balance: balance, charged: charged, share: share)
    }

    func testABalanceUnderTheCeilingIsSpentWhole() {
        XCTAssertEqual(cap(500, 2000), 500)
    }

    func testABalanceOverTheCeilingStopsAtTheShare() {
        XCTAssertEqual(cap(2000, 1000), 700)
    }

    func testABalanceExactlyAtTheCeilingIsSpentWhole() {
        XCTAssertEqual(cap(700, 1000), 700)
    }

    /// 70% of 33.33 is 23.331 — floored to whole minor units, as Stripe takes integers.
    func testTheCeilingIsFlooredToWholeMinorUnits() {
        XCTAssertEqual(cap(1000, 33.33), 23.33, accuracy: 0.000_001)
    }

    /// 100.10 × 0.7 is 70.07 on the server and 70.069… in binary floating point, which floors a cent
    /// short — the reason the arithmetic is decimal.
    func testTheFloorIsTakenOnTheDecimalTheWireCarried() {
        XCTAssertEqual(cap(1000, 100.10), 70.07, accuracy: 0.000_001)
    }

    /// The ruling, as a property over a spread of totals: the card always pays something.
    func testCreditNeverSettlesTheWholeBooking() {
        for charged in [1, 7, 99.99, 1000, 13333.33] {
            XCTAssertLessThan(cap(1_000_000, charged), charged, "charged \(charged)")
        }
    }

    func testNothingToSpendOrNothingToSpendItOnAnswersZero() {
        XCTAssertEqual(cap(0, 1000), 0)
        XCTAssertEqual(cap(-100, 1000), 0)
        XCTAssertEqual(cap(500, 0), 0)
        XCTAssertEqual(cap(500, 1000, 0), 0)
    }
}

/// What the confirm step states and the sticky bar charges when credit applies.
final class CreditSummaryTests: XCTestCase {
    private let quote = BookingQuote(
        totalPrice: 1500,
        currencyCode: "CZK",
        creditBalance: 250,
        creditMaxShareOfOrder: 0.7
    )

    func testACardBookingTakesTheCreditOffTheCardFigureOnly() {
        let summary = BookingPriceSummary.resolve(quote: quote, discount: 0, payByCard: true)

        XCTAssertEqual(summary.total, 1500, "the sale keeps its size")
        XCTAssertEqual(summary.creditApplied, 250)
        XCTAssertEqual(summary.dueOnCard, 1250)
    }

    func testCashTakesNoCreditButStillKnowsTheBalanceForItsHint() {
        let summary = BookingPriceSummary.resolve(quote: quote, discount: 0, payByCard: false)

        XCTAssertEqual(summary.creditApplied, 0)
        XCTAssertEqual(summary.dueOnCard, 1500)
        XCTAssertEqual(summary.creditBalance, 250)
    }

    /// The cap is taken on the CHARGED price, promo included, which the server's quote cannot know.
    func testTheCapIsTakenOnThePriceAfterThePromo() {
        let large = BookingQuote(totalPrice: 1000, currencyCode: "CZK", creditBalance: 5000, creditMaxShareOfOrder: 0.7)

        let summary = BookingPriceSummary.resolve(quote: large, discount: 200, payByCard: true)

        XCTAssertEqual(summary.total, 800)
        XCTAssertEqual(summary.creditApplied, 560)
        XCTAssertEqual(summary.dueOnCard, 240)
    }

    func testAQuoteWithoutABalanceAppliesNothing() {
        let none = BookingQuote(totalPrice: 1000, currencyCode: "CZK")

        XCTAssertEqual(BookingPriceSummary.resolve(quote: none, discount: 0, payByCard: true).creditApplied, 0)
    }
}

/// The credit copy takes the share and the dates from the server: a percentage or a duration written
/// into a translation drifts from the rule the backend enforces the first time the rule moves.
final class CreditCopyTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]
    private static let creditKeys = [
        "credit_your_credit", "credit_auto_applied_share", "credit_expires_on", "credit_none",
        "credit_explainer_title", "credit_explainer_source_title", "credit_explainer_source_body",
        "credit_explainer_spend_title", "credit_explainer_spend_body", "credit_explainer_points_title",
        "credit_explainer_points_body", "profile_row_credit", "booking_summary_due_on_card",
        "booking_summary_credit_note", "booking_summary_credit_card_only", "order_paid_with_credit",
        "order_paid_by_card"
    ]

    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    func testTheShareIsTheServersNumberInEveryLanguage() throws {
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            XCTAssertTrue(L10n.Credit.autoAppliedShare(0.55).contains("55"), language)
            XCTAssertTrue(L10n.Credit.explainerSpendBody(0.55).contains("55"), language)
        }
    }

    func testNoCreditRowStatesANumberOfItsOwn() throws {
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            for key in Self.creditKeys {
                let value = L10n.localized(key)
                XCTAssertNotEqual(value, key, "\(key) is missing in \(language)")
                XCTAssertNil(
                    value.replacingOccurrences(of: #"%\d+\$(lld|@)"#, with: "", options: .regularExpression)
                        .range(of: #"\d"#, options: .regularExpression),
                    "\(key) states a number of its own in \(language): \(value)"
                )
            }
        }
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
