import Foundation
import XCTest
@testable import CleansiaCustomer

/// The server writes four platform cancellation reasons and the detail turned three of them into a
/// sentence. The fourth, written by the unfilled-order sweep, reached the customer as a cancelled
/// order with no line saying why — the one no-show the platform can prove, and the one that pays
/// the apology credit, was the one left silent.
///
/// Read off the COMPILED string tables in the built app bundle rather than the `.xcstrings` source,
/// so a value that never reaches a device fails here too.
final class CancellationReasonCopyTests: XCTestCase {
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    /// Mirrors `Cleansia.Core.Domain.Orders.OrderCancellationReasons`.
    private let reasons = [
        "order.cancelled.payment_not_completed": "order_cancelled_reason_payment_not_completed",
        "order.cancelled.recurring_not_confirmed": "order_cancelled_reason_recurring_not_confirmed",
        "order.cancelled.company_wind_down": "order_cancelled_reason_company_wind_down",
        "order.cancelled.no_cleaner_available": "order_cancelled_reason_no_cleaner_available"
    ]

    private let noCleanerKey = "order_cancelled_reason_no_cleaner_available"

    /// The refund and the apology credit are each a line of their own on the detail, so the reason
    /// may not restate them — a second statement of the same money fact can drift from the first.
    private let moneyStems: [String: [String]] = [
        "en": ["refund", "credit", "paid"],
        "cs": ["vrac", "vrát", "kredit", "zaplat"],
        "sk": ["vrac", "vrát", "kredit", "zaplat"],
        "uk": ["поверн", "кредит", "бонус", "сплат"],
        "ru": ["возвр", "верн", "кредит", "бонус", "оплат"]
    ]

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

    func testEveryPlatformReasonRendersItsSentenceInEveryLocale() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            L10n.bundle = try localeBundle(language)
            for (reason, key) in reasons {
                let sentence = try XCTUnwrap(table[key], "\(language).lproj is missing \(key)")
                XCTAssertEqual(CancellationReasonCopy.text(for: reason), sentence, "\(reason) in \(language)")
            }
        }
    }

    func testTheUnfilledOrderSweepsReasonIsOneOfThem() throws {
        let table = try localizableTable(for: "en")
        L10n.bundle = try localeBundle("en")

        XCTAssertEqual(CancellationReasonCopy.text(for: "order.cancelled.no_cleaner_available"), table[noCleanerKey])
    }

    /// A newer server's key must not put `order.cancelled.something` on screen.
    func testAnUnknownReasonABlankOneAndNoneAtAllRenderNothing() throws {
        L10n.bundle = try localeBundle("en")

        XCTAssertNil(CancellationReasonCopy.text(for: nil))
        XCTAssertNil(CancellationReasonCopy.text(for: ""))
        XCTAssertNil(CancellationReasonCopy.text(for: "order.cancelled.something_newer"))
        XCTAssertNil(CancellationReasonCopy.text(for: "free text from an admin"))
    }

    func testTheNoCleanerSentenceIsTranslatedInAllFiveLocales() throws {
        let english = try XCTUnwrap(localizableTable(for: "en")[noCleanerKey])
        XCTAssertFalse(english.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
        for language in languages.dropFirst() {
            let translated = try XCTUnwrap(localizableTable(for: language)[noCleanerKey], "\(language).lproj")
            XCTAssertFalse(translated.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            XCTAssertNotEqual(translated, english, "\(language).lproj left \(noCleanerKey) in English")
        }
    }

    func testTheNoCleanerSentenceLeavesTheRefundAndTheCreditToTheirOwnLines() throws {
        for language in languages {
            let sentence = try XCTUnwrap(localizableTable(for: language)[noCleanerKey]).lowercased()
            for stem in moneyStems[language] ?? [] {
                XCTAssertFalse(sentence.contains(stem), "\(language).lproj states money (\(stem)) in the reason")
            }
        }
    }

    private func localizableTable(for language: String) throws -> [String: String] {
        let strings = try localeBundle(language).bundleURL.appendingPathComponent("Localizable.strings")
        return try XCTUnwrap(
            NSDictionary(contentsOf: strings) as? [String: String],
            "Localizable.strings unreadable for \(language)"
        )
    }

    private func localeBundle(_ language: String) throws -> Bundle {
        let lproj = try XCTUnwrap(
            appBundle.url(forResource: language, withExtension: "lproj"),
            "\(language).lproj missing from the app bundle"
        )
        return try XCTUnwrap(Bundle(url: lproj), "\(language).lproj at \(lproj.path) is not a bundle")
    }
}
