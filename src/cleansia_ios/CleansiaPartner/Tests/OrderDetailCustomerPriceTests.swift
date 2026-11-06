import XCTest

/// The detail never shows what the customer pays: the cleaner's figure is her own pay in the head, and on a cash job
/// the one amount it names is what the customer still owes at the door, the total less any credit — in the
/// head's flag, the cash block, the button and both confirms. A view's rows are not observable in a unit test, so
/// the gate is read from source.
final class OrderDetailCustomerPriceTests: XCTestCase {
    private static let views = [
        "OrderDetailContent.swift", "OrderDetailCards.swift", "JobRunSheet.swift", "JobHead.swift",
        "StickyActionFooter.swift", "OrderDetailView.swift"
    ]

    func testNoViewOfTheDetailReadsTheCustomersPrice() throws {
        for file in Self.views {
            let source = try compact(file)
            XCTAssertFalse(source.contains("payment.total"), "\(file) shows the customer's total")
            XCTAssertFalse(source.contains("payment.subtotal"), "\(file) shows the customer's subtotal")
            XCTAssertFalse(source.contains("pkg.price"), "\(file) shows a package's price")
            XCTAssertFalse(source.contains("PaymentCard("), "\(file) still draws the payment card")
        }
    }

    func testTheCashIsTheAmountDueEverywhereItIsSaid() throws {
        let model = try compact("OrderDetail.swift")
        XCTAssertTrue(model.contains("varcashDueLabel:String?{guardletdue=payment.amountDue,due>0"))
        XCTAssertTrue(model.contains("varcashNotPaidOwedLabel:String?{cashDueLabel}"))
        let footer = try compact("StickyActionFooter.swift")
        XCTAssertTrue(footer.contains("cashAmount.map(L10n.Orders.cashMarkAmount)"), "the button names the amount")
        XCTAssertTrue(try compact("OrderDetailContent.swift").contains("cashAmount:order.cashDueLabel"))
        XCTAssertTrue(try compact("OrderDetailCards.swift").contains("guardpayment.isCash,letcash=cashDueLabel"))
    }

    private func compact(_ file: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(
            contentsOf: root.appendingPathComponent("CleansiaPartner/Sources/Features/Orders/\(file)"),
            encoding: .utf8
        )
        .components(separatedBy: .whitespacesAndNewlines)
        .joined()
    }
}
