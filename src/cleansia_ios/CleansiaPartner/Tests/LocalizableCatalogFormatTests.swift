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
}
