import XCTest

/// On a cash job the customer's price is the amount the cleaner collects at the door; on a card job, or one whose
/// payment type did not arrive, the cleaner's figure is their own pay in the header, as on Android
/// (`showsCustomerPrice()`). A card's rows are not observable in a unit test, so the gate is read from source.
final class OrderDetailCustomerPriceTests: XCTestCase {
    func testThePaymentCardShowsTheBreakdownAndTheTotalOnlyOnACashJob() throws {
        let source = try compactCards()

        XCTAssertTrue(
            source.contains("ifpayment.isCash{ifpayment.hasBreakdown,letsubtotal=payment.subtotal{"),
            "the breakdown is not behind the cash gate"
        )
        XCTAssertTrue(
            source
                .contains("iflettotal=payment.total{paymentRow(L10n.Orders.paymentTotal,total,emphasis:true)}}HStack{"),
            "the total is not behind the cash gate, or the gate does not close before the method and status"
        )
        XCTAssertEqual(occurrences(of: "payment.subtotal", in: source), 1)
        XCTAssertEqual(occurrences(of: "payment.total", in: source), 1)
    }

    func testAPackagePriceShowsOnlyOnACashJob() throws {
        let source = try compactCards()

        XCTAssertTrue(source.contains("value:price(of:pkg)"), "a package line reads its price around the gate")
        XCTAssertTrue(
            source.contains("guardorder.payment.isCash,letprice=package.priceelse{returnnil}"),
            "a package price shows on a card job"
        )
        XCTAssertEqual(occurrences(of: ".price", in: source), 1)
    }

    private func occurrences(of needle: String, in source: String) -> Int {
        source.components(separatedBy: needle).count - 1
    }

    private func compactCards() throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(
            contentsOf: root.appendingPathComponent("CleansiaPartner/Sources/Features/Orders/OrderDetailCards.swift"),
            encoding: .utf8
        )
        .components(separatedBy: .whitespacesAndNewlines)
        .joined()
    }
}
