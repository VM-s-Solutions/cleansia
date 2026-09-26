import Foundation
import XCTest
@testable import CleansiaCustomer

/// The iOS twin of Android's `GuestOrderStringsTest`. Read through the BUILT `.lproj` tables, as
/// `PreferredOfferStringsTests` does, because a key in the catalog but absent from a shipped language
/// renders as its own name on screen.
final class GuestOrderStringsTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    private let keys = [
        "guest_order_entry",
        "guest_order_title",
        "guest_order_intro",
        "guest_order_link",
        "guest_order_lookup",
        "guest_order_required",
        "guest_order_empty",
        "guest_order_loading",
        "guest_order_cancel",
        "guest_order_cannot_cancel",
        "guest_order_preview_required",
        "guest_order_fee_estimate"
    ]

    /// The guest fee copy must call the refund a maximum, tie it to collected card payments and point at
    /// the confirmation for the real figure — the review finding the Android and web copy closed.
    private let feeQualifiers = [
        "en": ["Maximum", "collected card", "actual amount"],
        "cs": ["Maximální", "přijaté platby kartou", "skutečně vrácenou"],
        "sk": ["Maximálne", "prijaté platby kartou", "skutočne vrátenú"],
        "uk": ["Максимальне", "отримані платежі карткою", "фактично повернену"],
        "ru": ["Максимальный", "полученные платежи картой", "фактически возвращённая"]
    ]

    func testEveryGuestStringIsWrittenInAllFiveLanguages() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in keys {
                let value = table[key]
                XCTAssertNotNil(value, "\(key) missing from \(language).lproj")
                XCTAssertFalse(
                    value?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ?? true,
                    "\(language).lproj leaves \(key) empty"
                )
            }
        }
    }

    func testTheFourTranslationsAreNotTheEnglishStringCopiedOver() throws {
        let english = try localizableTable(for: "en")
        for language in languages.dropFirst() {
            let table = try localizableTable(for: language)
            for key in keys {
                XCTAssertNotEqual(table[key], english[key], "\(language).lproj left \(key) in English")
            }
        }
    }

    func testThePreviewRequiredPromptInterpolatesNothing() throws {
        for language in languages {
            let value = try XCTUnwrap(localizableTable(for: language)["guest_order_preview_required"])
            XCTAssertFalse(value.contains("%"), "\(language) interpolates into the prompt: \"\(value)\"")
        }
    }

    func testTheGuestFeeCopyQualifiesTheMaximumRefundAndCollectedPaymentsInEveryLanguage() throws {
        for language in languages {
            let value = try XCTUnwrap(localizableTable(for: language)["guest_order_fee_estimate"])
            XCTAssertTrue(value.contains("%1$@"), "\(language) lost the fee placeholder: \"\(value)\"")
            XCTAssertTrue(value.contains("%2$@"), "\(language) lost the refund placeholder: \"\(value)\"")
            for word in feeQualifiers[language] ?? [] {
                XCTAssertTrue(value.contains(word), "\(language) never says \"\(word)\": \"\(value)\"")
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
