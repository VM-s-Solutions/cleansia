import Foundation
import XCTest
@testable import CleansiaCustomer

/// The contract for work binds the operating company and the cleaner, so the customer app carries none of
/// its copy: the booking names the customer's own contract, with the company on its terms, and asks for the
/// request to start within the withdrawal period. The sentence's link placeholder is pinned by the Core
/// consent catalog test, beside the consent sentences it is the twin of.
///
/// Read through the BUILT `.lproj` tables rather than the `.xcstrings` source, because a key present in
/// the catalog but absent from a shipped language renders as its own name on screen.
final class WorkContractStringsTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    private let required = [
        "booking_contract_notice",
        "consent_early_performance_draft_2026_09_29",
        "booking_early_performance_toggle"
    ]

    func testEveryBookingContractStringIsWrittenInAllFiveLanguages() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in required {
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
            for key in required {
                XCTAssertNotEqual(table[key], english[key], "\(language).lproj left \(key) in English")
            }
        }
    }

    func testNoLanguageShipsContractForWorkCopyOrLinksItsText() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            XCTAssertEqual(
                table.keys.filter { $0.hasPrefix("work_contract_") }.sorted(),
                [],
                "\(language).lproj still ships the customer's contract-for-work screen"
            )
            let notice = try XCTUnwrap(table["booking_contract_notice"], "booking_contract_notice in \(language)")
            XCTAssertFalse(notice.contains("cleansia://work-contract"), "\(language) links the contract for work")
        }
    }

    /// The tick names the period it is about and links nothing: it is the wording the server records by
    /// version, not a pointer to another text.
    func testTheEarlyPerformanceRequestNamesTheFourteenDaysAndCarriesNoLink() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            let request = try XCTUnwrap(table["consent_early_performance_draft_2026_09_29"], language)
            XCTAssertTrue(request.contains("14"), "\(language) drops the 14-day period: \"\(request)\"")
            XCTAssertFalse(request.contains("cleansia://"), "\(language) links a page from the tick: \"\(request)\"")
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
