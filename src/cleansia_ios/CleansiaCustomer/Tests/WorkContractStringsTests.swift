import CleansiaCore
import Foundation
import XCTest
@testable import CleansiaCustomer

/// The copy of the customer's contract surfaces: the wizard sentence, the detail line, and the
/// contract screen. A key with no row in a locale falls back to English without failing the build,
/// and the roster is the only thing that reads all five tables. The sentence's link placeholder is
/// pinned by the Core consent catalog test, beside the consent sentences it is the twin of.
///
/// Read through the BUILT `.lproj` tables rather than the `.xcstrings` source, because a key present in
/// the catalog but absent from a shipped language renders as its own name on screen.
final class WorkContractStringsTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    private let required = [
        "booking_work_contract_notice",
        "work_contract_title",
        "work_contract_accepted_line",
        "work_contract_read",
        "work_contract_version",
        "work_contract_facts_title",
        "work_contract_order_number",
        "work_contract_window",
        "work_contract_price",
        "work_contract_location",
        "work_contract_accepted_on",
        "work_contract_accepted_in_language",
        "work_contract_load_error"
    ]

    func testEveryContractStringIsWrittenInAllFiveLanguages() throws {
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

    /// A dropped positional argument renders as literal text in one language only; `%@`, never `%s`,
    /// which prints garbage for a Swift `String`.
    func testEveryPlaceholderSurvivesTranslation() throws {
        let expected = [
            "work_contract_accepted_line": ["%1$@", "%2$@", "%3$@"],
            "work_contract_version": ["%1$@"],
            "work_contract_accepted_on": ["%1$@", "%2$@"],
            "work_contract_accepted_in_language": ["%1$@"]
        ]
        for language in languages {
            let table = try localizableTable(for: language)
            for (key, placeholders) in expected {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)")
                for placeholder in placeholders {
                    XCTAssertTrue(value.contains(placeholder), "\(language)/\(key) lost \(placeholder): \"\(value)\"")
                }
                XCTAssertFalse(value.contains("$s"), "\(language)/\(key) carries an Android specifier: \"\(value)\"")
            }
        }
    }

    /// The line names the cleaner the crew card names, in every language, through the real accessor.
    func testTheAcceptedLineCarriesTheNameTheDateAndTheVersionInEveryLanguage() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for language in languages {
            L10n.bundle = try localeBundle(language)
            let line = L10n.WorkContract.acceptedLine("NAME-SENTINEL", "DATE-SENTINEL", "VERSION-SENTINEL")
            XCTAssertTrue(line.contains("NAME-SENTINEL"), "\(language) drops the cleaner's name")
            XCTAssertTrue(line.contains("DATE-SENTINEL"), "\(language) drops the instant")
            XCTAssertTrue(line.contains("VERSION-SENTINEL"), "\(language) drops the version")
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
