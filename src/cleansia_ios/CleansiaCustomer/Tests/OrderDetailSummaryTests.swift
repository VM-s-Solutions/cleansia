import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The two summaries under the order-detail hero: what the customer is charged
/// (`PriceBreakdownCard`) and the facts the live progress hero leaves out
/// (`OrderMetaStrip(showFacts = liveHero)`).
final class OrderDetailSummaryTests: XCTestCase {
    // MARK: - Subtotal row

    func testSubtotalIsHiddenWhenNothingWasDiscounted() {
        let order = OrderItem(totalPrice: 2100, originalSubtotal: 2100)
        XCTAssertNil(OrderPriceBreakdown.resolve(order).subtotal)
    }

    func testSubtotalIsHiddenWhenTheWireCarriesNone() {
        let order = OrderItem(totalPrice: 2100, originalSubtotal: 0)
        XCTAssertNil(OrderPriceBreakdown.resolve(order).subtotal)
    }

    func testSubtotalShowsWhenItDiffersFromTheTotal() {
        let order = OrderItem(totalPrice: 1890, originalSubtotal: 2100)
        XCTAssertEqual(OrderPriceBreakdown.resolve(order).subtotal, 2100)
        XCTAssertEqual(OrderPriceBreakdown.resolve(order).total, 1890)
    }

    // MARK: - Discount lines

    func testEveryNonZeroSourceGetsItsOwnLineInAndroidsOrder() {
        let order = OrderItem(
            totalPrice: 1400,
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
        let order = OrderItem(
            totalPrice: 1890,
            originalSubtotal: 2100,
            appliedDiscountSource: ._4,
            tierDiscountAmount: 210,
            membershipDiscountAmount: 0
        )
        let discounts = OrderPriceBreakdown.resolve(order).discounts
        XCTAssertEqual(discounts, [.init(source: .tier, amount: 210)])
        XCTAssertFalse(discounts.contains { $0.source == .membership })
    }

    func testAbsentAndNegativeAmountsNeverRenderALine() {
        let order = OrderItem(totalPrice: 2100, originalSubtotal: 2100, tierDiscountAmount: -5)
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
        XCTAssertNil(OrderPriceBreakdown.resolve(OrderItem()).paymentMethod)
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
        XCTAssertNil(OrderPriceBreakdown.resolve(OrderItem()).paymentStatus)
    }

    // MARK: - Hero facts

    func testTheCodeIsCarriedForwardSoTheLiveHeroCanShowIt() {
        let order = OrderItem(confirmationCode: "CLN-777")
        XCTAssertEqual(OrderHeroFacts.resolve(order).confirmationCode, "CLN-777")
    }

    func testABlankCodeIsNoCode() {
        XCTAssertNil(OrderHeroFacts.resolve(OrderItem(confirmationCode: "  ")).confirmationCode)
        XCTAssertNil(OrderHeroFacts.resolve(OrderItem()).confirmationCode)
    }

    func testTheStruckSubtotalNeedsBothADiscountSourceAndAHigherSubtotal() {
        XCTAssertNil(OrderHeroFacts.resolve(
            OrderItem(totalPrice: 1890, originalSubtotal: 2100, appliedDiscountSource: ._0)
        ).struckSubtotal)
        XCTAssertNil(OrderHeroFacts.resolve(
            OrderItem(totalPrice: 2100, originalSubtotal: 2100, appliedDiscountSource: ._1)
        ).struckSubtotal)
        XCTAssertEqual(OrderHeroFacts.resolve(
            OrderItem(totalPrice: 1890, originalSubtotal: 2100, appliedDiscountSource: ._1)
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
        OrderPriceBreakdown.resolve(OrderItem(paymentType: Code(name: name, value: value))).paymentMethod
    }

    private func status(value: Int, name: String? = nil) -> OrderPaymentStatus? {
        OrderPriceBreakdown.resolve(OrderItem(paymentStatus: Code(name: name, value: value))).paymentStatus
    }

    private func chips(_ source: AppliedDiscountSource) -> [OrderDiscountSource] {
        OrderDiscountSource.chips(for: source)
    }
}
