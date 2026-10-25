import CleansiaCore
import Foundation
import XCTest
@testable import CleansiaPartner

/// "Cancel job" leaves the crew; it cancels nothing for the customer. Read through the BUILT `.lproj` tables,
/// because a key present in the catalog but missing from a shipped language renders as its own name.
final class DropOrderStringsTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.partner") ?? .main
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    private let keys = [
        "order_drop_action",
        "order_drop_confirm_title",
        "order_drop_confirm_message",
        "order_drop_confirm_action",
        "order_drop_keep",
        "order_dropped_toast_no_crew",
        "order_dropped_toast"
    ]

    func testEveryDropStringIsWrittenInAllFiveLanguages() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in keys {
                let value = table[key]
                XCTAssertNotNil(value, "\(key) missing from \(language).lproj")
                XCTAssertFalse(value?.isEmpty ?? true, "\(language).lproj leaves \(key) empty")
            }
        }
    }

    /// Nothing guarantees anyone is told: a company with no administrator in the audience hears nothing.
    func testNoDropStringPromisesThatTheCompanyIsTold() throws {
        let companyWords = [
            "en": ["admin", "company"],
            "cs": ["admin", "společnost"],
            "sk": ["admin", "spoločnos"],
            "uk": ["адмін", "компані"],
            "ru": ["админ", "компани"]
        ]
        for language in languages {
            let table = try localizableTable(for: language)
            for key in keys {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)").lowercased()
                for word in companyWords[language] ?? [] {
                    XCTAssertFalse(
                        value.contains(word),
                        "\(language)/\(key) promises the company is told: \"\(value)\""
                    )
                }
            }
        }
    }

    /// The way out of "Cancel this job?" is never a button that also reads "Cancel".
    func testTheWayOutOfTheConfirmIsNotTheGenericCancel() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            XCTAssertNotEqual(table["order_drop_keep"], table["cancel"], "\(language) labels the way out Cancel")
        }
    }

    private func localizableTable(for language: String) throws -> [String: String] {
        let lproj = try XCTUnwrap(
            appBundle.url(forResource: language, withExtension: "lproj"),
            "\(language).lproj missing from the app bundle"
        )
        let strings = lproj.appendingPathComponent("Localizable.strings")
        return try XCTUnwrap(
            NSDictionary(contentsOf: strings) as? [String: String],
            "Localizable.strings unreadable for \(language)"
        )
    }
}
