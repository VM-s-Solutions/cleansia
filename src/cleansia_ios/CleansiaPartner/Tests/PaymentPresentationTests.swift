import XCTest
@testable import CleansiaPartner

final class PaymentPresentationTests: XCTestCase {
    private let placeholder = "—"

    func testEveryPaymentTypeMapsToItsLocalizedLabel() {
        XCTAssertEqual(PaymentPresentation.methodLabel(1), L10n.Orders.paymentMethodCash)
        XCTAssertEqual(PaymentPresentation.methodLabel(2), L10n.Orders.paymentMethodCard)
    }

    func testEveryPaymentStatusMapsToItsLocalizedLabel() {
        XCTAssertEqual(PaymentPresentation.statusLabel(1), L10n.Orders.paymentStatusPending)
        XCTAssertEqual(PaymentPresentation.statusLabel(2), L10n.Orders.paymentStatusPaid)
        XCTAssertEqual(PaymentPresentation.statusLabel(3), L10n.Orders.paymentStatusFailed)
        XCTAssertEqual(PaymentPresentation.statusLabel(4), L10n.Orders.paymentStatusRefunded)
        XCTAssertEqual(PaymentPresentation.statusLabel(5), L10n.Orders.paymentStatusDisputed)
        XCTAssertEqual(PaymentPresentation.statusLabel(6), L10n.Orders.paymentStatusPartiallyRefunded)
    }

    func testEveryKnownValueYieldsRealCopy() {
        let labels = PaymentTypeCode.allCases.map { PaymentPresentation.methodLabel($0.rawValue) }
            + PaymentStatusCode.allCases.map { PaymentPresentation.statusLabel($0.rawValue) }
        for label in labels {
            XCTAssertFalse(label.isEmpty)
            XCTAssertNotEqual(label, placeholder)
        }
        XCTAssertEqual(Set(labels).count, labels.count)
    }

    func testSeverityBuckets() {
        XCTAssertEqual(PaymentPresentation.statusSeverity(PaymentStatusCode.paid.rawValue), .success)
        XCTAssertEqual(PaymentPresentation.statusSeverity(PaymentStatusCode.pending.rawValue), .warning)
        XCTAssertEqual(PaymentPresentation.statusSeverity(PaymentStatusCode.failed.rawValue), .error)
        XCTAssertEqual(PaymentPresentation.statusSeverity(PaymentStatusCode.disputed.rawValue), .error)
        XCTAssertEqual(PaymentPresentation.statusSeverity(PaymentStatusCode.refunded.rawValue), .neutral)
        XCTAssertEqual(
            PaymentPresentation.statusSeverity(PaymentStatusCode.partiallyRefunded.rawValue),
            .neutral
        )
        XCTAssertEqual(PaymentPresentation.statusSeverity(99), .neutral)
        XCTAssertEqual(PaymentPresentation.statusSeverity(nil), .neutral)
    }

    func testUnknownValueFallsBackToDiagnosticInDebugElsePlaceholder() {
        #if DEBUG
            XCTAssertEqual(PaymentPresentation.statusLabel(99), "#99")
            XCTAssertEqual(PaymentPresentation.methodLabel(99), "#99")
        #else
            XCTAssertEqual(PaymentPresentation.statusLabel(99), placeholder)
            XCTAssertEqual(PaymentPresentation.methodLabel(99), placeholder)
        #endif
    }

    func testMissingValueIsPlaceholder() {
        XCTAssertEqual(PaymentPresentation.statusLabel(nil), placeholder)
        XCTAssertEqual(PaymentPresentation.methodLabel(nil), placeholder)
    }
}
