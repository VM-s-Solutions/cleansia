import Foundation
import XCTest
@testable import CleansiaCustomer

final class DisputeLabelsTests: XCTestCase {
    private let languages = ["en", "cs", "sk", "uk", "ru"]
    private let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main
    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    func testEveryReasonAndStatusResolvesInAllFiveLocales() throws {
        for language in languages {
            L10n.bundle = try localeBundle(language)
            for value in 1 ... 8 {
                let label = L10n.Disputes.reason(value)
                XCTAssertFalse(label.hasPrefix("dispute_"), "reason \(value) in \(language)")
                XCTAssertNotEqual(label, "—", "reason \(value) in \(language)")
            }
            for value in 1 ... 6 {
                let label = L10n.Disputes.status(value)
                XCTAssertFalse(label.hasPrefix("dispute_"), "status \(value) in \(language)")
                XCTAssertNotEqual(label, "—", "status \(value) in \(language)")
            }
        }
    }

    func testAChargebackIsNotOther() throws {
        L10n.bundle = try localeBundle("en")
        XCTAssertEqual(L10n.Disputes.reason(8), "Chargeback")
        XCTAssertEqual(L10n.Disputes.reason(7), "Other")
    }

    func testAnUnknownValueShowsADash() {
        for value in [0, 9, 99] as [Int?] + [nil] {
            XCTAssertEqual(L10n.Disputes.reason(value), "—")
            XCTAssertEqual(L10n.Disputes.status(value), "—")
        }
    }

    func testThePartiallyRefundedLabelResolvesInAllFiveLocales() throws {
        for language in languages {
            L10n.bundle = try localeBundle(language)
            XCTAssertNotEqual(
                L10n.OrderDetail.paymentStatusLabel(.partiallyRefunded),
                "orders_payment_partially_refunded",
                language
            )
        }
    }

    private func localeBundle(_ language: String) throws -> Bundle {
        let lproj = try XCTUnwrap(appBundle.url(forResource: language, withExtension: "lproj"))
        return try XCTUnwrap(Bundle(url: lproj))
    }
}
