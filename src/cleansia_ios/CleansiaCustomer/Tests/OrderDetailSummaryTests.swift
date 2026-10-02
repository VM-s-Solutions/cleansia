import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The two summaries under the order-detail hero: what the customer is charged
/// (`PriceBreakdownCard`) and the facts the live progress hero leaves out
/// (`OrderMetaStrip(showFacts = liveHero)`).
final class OrderDetailSummaryTests: XCTestCase {
    // MARK: - Subtotal row

    func testSubtotalIsHiddenWhenNothingWasDiscounted() {
        let order = OrderFixtures.detail(total: 2100, originalSubtotal: 2100)
        XCTAssertNil(OrderPriceBreakdown.resolve(order).subtotal)
    }

    func testSubtotalIsHiddenWhenTheWireCarriesNone() {
        let order = OrderFixtures.detail(total: 2100, originalSubtotal: 0)
        XCTAssertNil(OrderPriceBreakdown.resolve(order).subtotal)
    }

    func testSubtotalShowsWhenItDiffersFromTheTotal() {
        let order = OrderFixtures.detail(total: 1890, originalSubtotal: 2100)
        XCTAssertEqual(OrderPriceBreakdown.resolve(order).subtotal, 2100)
        XCTAssertEqual(OrderPriceBreakdown.resolve(order).total, 1890)
    }

    // MARK: - Dirtiness line

    /// The receipt itemises the surcharge the order stored; the breakdown carries the same figure and
    /// the level it was charged for, never one derived from a rate, under a subtotal stated without it.
    func testTheStoredSurchargeIsItsOwnRowUnderASubtotalWithoutIt() {
        let order = OrderFixtures.detail(
            dirtiness: .increased,
            dirtinessSurchargeAmount: 480,
            total: 2080,
            originalSubtotal: 2080
        )
        let breakdown = OrderPriceBreakdown.resolve(order)
        XCTAssertEqual(breakdown.dirtiness, .increased)
        XCTAssertEqual(breakdown.dirtinessSurcharge, 480)
        XCTAssertEqual(breakdown.subtotal, 1600, "nothing was discounted, yet the surcharge needs a base")
    }

    func testTheRowsAddUpToTheTotalWhenADiscountCameOff() {
        let order = OrderFixtures.detail(
            dirtiness: .heavy,
            dirtinessSurchargeAmount: 600,
            total: 1400,
            originalSubtotal: 1600,
            tierDiscountAmount: 200
        )
        let breakdown = OrderPriceBreakdown.resolve(order)
        let discounts = breakdown.discounts.reduce(0.0) { $0 + $1.amount }
        XCTAssertEqual(breakdown.subtotal, 1000)
        XCTAssertEqual((breakdown.subtotal ?? 0) + breakdown.dirtinessSurcharge - discounts, breakdown.total)
    }

    // MARK: - Credit tender

    /// Credit is a tender: the total keeps the size of the sale, and the two figures say how it was
    /// paid — read off the order, never re-derived from the balance.
    func testAnOrderCreditPaidPartOfStatesBothTenders() {
        let order = OrderFixtures.detail(total: 1500, creditAppliedAmount: 320, amountDueOnCard: 1180)
        let breakdown = OrderPriceBreakdown.resolve(order)
        XCTAssertEqual(breakdown.total, 1500)
        XCTAssertEqual(breakdown.paidWithCredit, 320)
        XCTAssertEqual(breakdown.paidByCard, 1180)
    }

    /// Under a credit split the card line claims a payment only once the card was charged: a card order
    /// whose payment is pending or failed still has its card share to pay. The server's
    /// `Order.TookNoPayment`, Android's `cardShareLabelRes` and the web's rule.
    func testTheCardShareReadsPaidOnlyOnceTheCardWasCharged() {
        func label(_ status: Int) -> String {
            OrderFixtures.detail(
                creditAppliedAmount: 320,
                amountDueOnCard: 1180,
                paymentType: Code(type: "PaymentType", name: nil, value: 2),
                paymentStatus: Code(type: "PaymentStatus", name: nil, value: status)
            ).cardShareLabel
        }
        XCTAssertNotEqual(L10n.Credit.paidByCard, L10n.Credit.dueOnCard)
        for charged in [2, 4, 5, 6] {
            XCTAssertEqual(label(charged), L10n.Credit.paidByCard, "status \(charged)")
        }
        for uncharged in [1, 3] {
            XCTAssertEqual(label(uncharged), L10n.Credit.dueOnCard, "status \(uncharged)")
        }
    }

    func testAnOrderNoCreditTouchedStatesNoTender() {
        let order = OrderFixtures.detail(total: 1500)
        XCTAssertEqual(OrderPriceBreakdown.resolve(order).paidWithCredit, 0)
    }

    // MARK: - Discount lines

    func testEveryNonZeroSourceGetsItsOwnLineInAndroidsOrder() {
        let order = OrderFixtures.detail(
            total: 1400,
            originalSubtotal: 2100,
            tierDiscountAmount: 210,
            membershipDiscountAmount: 300,
            promoDiscountAmount: 190
        )
        XCTAssertEqual(
            OrderPriceBreakdown.resolve(order).discounts,
            [
                .init(source: .tier, amount: 210),
                .init(source: .membership, amount: 300),
                .init(source: .promo, amount: 190)
            ]
        )
    }

    /// The reason the card exists. At the top loyalty tier Plus adds nothing on
    /// top, so a member who paid for it sees a struck-through subtotal and no
    /// membership line — the only place the app can say where the money went.
    func testTopTierPlusMemberSeesTheTierLineAndNoMembershipLine() {
        let order = OrderFixtures.detail(
            total: 1890,
            originalSubtotal: 2100,
            tierDiscountAmount: 210,
            membershipDiscountAmount: 0,
            appliedDiscountSource: ._4
        )
        let discounts = OrderPriceBreakdown.resolve(order).discounts
        XCTAssertEqual(discounts, [.init(source: .tier, amount: 210)])
        XCTAssertFalse(discounts.contains { $0.source == .membership })
    }

    func testAbsentAndNegativeAmountsNeverRenderALine() {
        let order = OrderFixtures.detail(total: 2100, originalSubtotal: 2100, tierDiscountAmount: -5)
        XCTAssertTrue(OrderPriceBreakdown.resolve(order).discounts.isEmpty)
    }

    // MARK: - Payment method

    func testPaymentMethodMapsTheBackendCodes() {
        XCTAssertEqual(method(value: 1), .cash)
        XCTAssertEqual(method(value: 2), .card)
    }

    func testAnUnmappedPaymentMethodFallsBackToTheWireName() {
        XCTAssertEqual(method(value: 9, name: "BankTransfer"), .named("BankTransfer"))
    }

    func testAPaymentMethodWithNoCodeAndNoNameIsNotRendered() {
        XCTAssertNil(method(value: 9))
        XCTAssertNil(OrderPriceBreakdown.resolve(OrderFixtures.detail()).paymentMethod)
    }

    // MARK: - Payment status

    func testPaymentStatusMapsTheBackendCodes() {
        XCTAssertEqual(status(value: 1), .pending)
        XCTAssertEqual(status(value: 2), .paid)
        XCTAssertEqual(status(value: 3), .failed)
        XCTAssertEqual(status(value: 4), .refunded)
        XCTAssertEqual(status(value: 5), .disputed)
    }

    /// `PartiallyRefunded` (6) has no label on either platform, so it falls back
    /// to the wire name rather than reading as one of the five above.
    func testPartiallyRefundedFallsBackToTheWireName() {
        XCTAssertEqual(status(value: 6, name: "PartiallyRefunded"), .named("PartiallyRefunded"))
    }

    func testAPaymentStatusWithNoCodeAndNoNameIsNotRendered() {
        XCTAssertNil(status(value: 6))
        XCTAssertNil(OrderPriceBreakdown.resolve(OrderFixtures.detail()).paymentStatus)
    }

    // MARK: - Hero facts

    func testTheStruckSubtotalNeedsBothADiscountSourceAndAHigherSubtotal() {
        XCTAssertNil(OrderHeroFacts.resolve(
            OrderFixtures.detail(total: 1890, originalSubtotal: 2100, appliedDiscountSource: ._0)
        ).struckSubtotal)
        XCTAssertNil(OrderHeroFacts.resolve(
            OrderFixtures.detail(total: 2100, originalSubtotal: 2100, appliedDiscountSource: ._1)
        ).struckSubtotal)
        XCTAssertEqual(OrderHeroFacts.resolve(
            OrderFixtures.detail(total: 1890, originalSubtotal: 2100, appliedDiscountSource: ._1)
        ).struckSubtotal, 2100)
    }

    func testEachDiscountSourceCodeNamesItsOwnChips() {
        XCTAssertEqual(chips(._0), [])
        XCTAssertEqual(chips(._1), [.tier])
        XCTAssertEqual(chips(._2), [.membership])
        XCTAssertEqual(chips(._3), [.promo])
        XCTAssertEqual(chips(._4), [.membership, .tier])
        XCTAssertEqual(OrderDiscountSource.chips(for: nil), [])
    }

    // MARK: - Fixtures

    private func method(value: Int, name: String? = nil) -> OrderPaymentMethod? {
        OrderPriceBreakdown.resolve(OrderFixtures.detail(paymentType: Code(name: name, value: value))).paymentMethod
    }

    private func status(value: Int, name: String? = nil) -> OrderPaymentStatus? {
        OrderPriceBreakdown.resolve(OrderFixtures.detail(paymentStatus: Code(name: name, value: value))).paymentStatus
    }

    private func chips(_ source: AppliedDiscountSource) -> [OrderDiscountSource] {
        OrderDiscountSource.chips(for: source)
    }
}
