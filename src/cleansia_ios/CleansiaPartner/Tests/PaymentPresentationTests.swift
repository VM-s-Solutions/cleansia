import XCTest
@testable import CleansiaPartner

final class PaymentPresentationTests: XCTestCase {
    func testMethodLabelMapsCashAndCardToLocalizedKeys() {
        XCTAssertEqual(PaymentPresentation.methodLabel("Cash"), L10n.Orders.paymentMethodCash)
        XCTAssertEqual(PaymentPresentation.methodLabel("card"), L10n.Orders.paymentMethodCard)
        XCTAssertEqual(PaymentPresentation.methodLabel("CreditCard"), L10n.Orders.paymentMethodCard)
    }

    func testMethodLabelUnknownFallsBackInDebugElseDash() {
        let result = PaymentPresentation.methodLabel("Bitcoin")
        #if DEBUG
            XCTAssertEqual(result, "Bitcoin")
        #else
            XCTAssertEqual(result, "—")
        #endif
    }

    func testMethodLabelEmptyAndNilAreDash() {
        XCTAssertEqual(PaymentPresentation.methodLabel("   "), "—")
        XCTAssertEqual(PaymentPresentation.methodLabel(nil), "—")
    }

    func testStatusLabelMapsBuckets() {
        XCTAssertEqual(PaymentPresentation.statusLabel("Paid"), L10n.Orders.paymentStatusPaid)
        XCTAssertEqual(PaymentPresentation.statusLabel("Pending"), L10n.Orders.paymentStatusPending)
        XCTAssertEqual(PaymentPresentation.statusLabel("Refunded"), L10n.Orders.paymentStatusFailed)
        XCTAssertEqual(PaymentPresentation.statusLabel("Declined"), L10n.Orders.paymentStatusFailed)
    }

    func testStatusLabelUnknownFallsBackInDebugElseDash() {
        let result = PaymentPresentation.statusLabel("Weird")
        #if DEBUG
            XCTAssertEqual(result, "Weird")
        #else
            XCTAssertEqual(result, "—")
        #endif
    }
}
