import Foundation
import XCTest

/// The string catalogs are hand-managed JSON ported from the Android apps;
/// Android-style `%1$s` placeholders slip through compiles but render as
/// literal text via `String(format:)` — Foundation needs `%1$@` for strings.
/// Scans the source catalogs of all three packages so the residue cannot
/// come back.
final class LocalizableCatalogFormatTests: XCTestCase {
    private static let catalogPaths = [
        "CleansiaPartner/Resources/Localizable.xcstrings",
        "CleansiaCustomer/Resources/Localizable.xcstrings",
        "CleansiaCore/Sources/CleansiaCore/Resources/Localizable.xcstrings"
    ]

    private static let locales = ["en", "cs", "sk", "uk", "ru"]

    /// A push is addressed to a USER's device tokens, not to an app, so an event only one audience
    /// ever renders still lands on the sibling bundle — and borrows the owning audience's wording
    /// rather than being re-voiced. Deliberately NOT the whole cross-audience set:
    /// `order.cleaner_assigned` is customer-targeted and ships per-audience wording today, so a
    /// wider roster would fail on copy nobody has agreed to change.
    private static let borrowedWordingEvents = [
        "order.starting_soon",
        "order.assigned",
        "order.assignment_revoked",
        "order.preferred_offer_closed"
    ]

    private func iosRoot() -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }

    func testCatalogsCarryNoAndroidStylePositionalStringSpecifiers() throws {
        let pattern = try NSRegularExpression(pattern: "%\\d+\\$s")
        for path in Self.catalogPaths {
            let url = iosRoot().appendingPathComponent(path)
            let text = try String(contentsOf: url, encoding: .utf8)
            let matches = pattern.matches(in: text, range: NSRange(text.startIndex..., in: text))
            XCTAssertTrue(
                matches.isEmpty,
                "\(path) carries \(matches.count) Android-style %N$s placeholder(s) — use %N$@ for strings"
            )
        }
    }

    func testCatalogsParseAsJson() throws {
        for path in Self.catalogPaths {
            let url = iosRoot().appendingPathComponent(path)
            let data = try Data(contentsOf: url)
            XCTAssertNoThrow(
                try JSONSerialization.jsonObject(with: data),
                "\(path) is not valid JSON"
            )
        }
    }

    func testBorrowedWordingIsIdenticalInBothAppCatalogs() throws {
        let partner = try catalogStrings(at: Self.catalogPaths[0])
        let customer = try catalogStrings(at: Self.catalogPaths[1])
        XCTAssertFalse(Self.borrowedWordingEvents.isEmpty, "the roster is empty — this guard asserts nothing")

        for event in Self.borrowedWordingEvents {
            for key in ["push.\(event).title", "push.\(event).body"] {
                for locale in Self.locales {
                    let owned = try XCTUnwrap(
                        value(of: key, in: partner, locale: locale),
                        "\(key) [\(locale)] · partner"
                    )
                    let borrowed = try XCTUnwrap(
                        value(of: key, in: customer, locale: locale),
                        "\(key) [\(locale)] · customer"
                    )
                    XCTAssertEqual(owned, borrowed, "\(key) [\(locale)] diverges between the two app catalogs")
                }
            }
        }
    }

    private func catalogStrings(at path: String) throws -> [String: Any] {
        let data = try Data(contentsOf: iosRoot().appendingPathComponent(path))
        let root = try JSONSerialization.jsonObject(with: data) as? [String: Any]
        return try XCTUnwrap(root?["strings"] as? [String: Any], "\(path) carries no strings table")
    }

    private func value(of key: String, in strings: [String: Any], locale: String) -> String? {
        let entry = strings[key] as? [String: Any]
        let localizations = entry?["localizations"] as? [String: Any]
        let localization = localizations?[locale] as? [String: Any]
        let stringUnit = localization?["stringUnit"] as? [String: Any]
        return stringUnit?["value"] as? String
    }
}
