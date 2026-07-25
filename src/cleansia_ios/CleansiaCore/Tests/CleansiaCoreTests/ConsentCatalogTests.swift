import Foundation
import XCTest
@testable import CleansiaCore

/// The consent sentence renders as plain text when its markup is missing, so a
/// translation that dropped a link would silently unpublish a legal document
/// instead of failing. Pin the markup in every locale of both apps.
final class ConsentCatalogTests: XCTestCase {
    private static let consentKeys = [
        ("CleansiaCustomer/Resources/Localizable.xcstrings", "register_terms_and_conditions"),
        ("CleansiaPartner/Resources/Localizable.xcstrings", "accept_terms")
    ]

    private static let locales = ["en", "cs", "sk", "uk", "ru"]

    private func iosRoot() -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }

    private func value(catalog: String, key: String, locale: String) throws -> String {
        let data = try Data(contentsOf: iosRoot().appendingPathComponent(catalog))
        let json = try JSONSerialization.jsonObject(with: data) as? [String: Any]
        let strings = json?["strings"] as? [String: Any]
        let entry = strings?[key] as? [String: Any]
        let localizations = entry?["localizations"] as? [String: Any]
        let unit = (localizations?[locale] as? [String: Any])?["stringUnit"] as? [String: Any]
        return try XCTUnwrap(unit?["value"] as? String, "\(catalog) has no \(locale) value for \(key)")
    }

    func testBothConsentSentencesLinkTermsAndPrivacyInEveryLocale() throws {
        for (catalog, key) in Self.consentKeys {
            for locale in Self.locales {
                let sentence = try value(catalog: catalog, key: key, locale: locale)
                for link in ConsentLink.allCases {
                    XCTAssertTrue(
                        sentence.contains("(\(link.rawValue))"),
                        "\(catalog) \(key) [\(locale)] is missing the \(link.rawValue) link: \(sentence)"
                    )
                }
                let rendered = ConsentMarkdown.attributed(sentence)
                XCTAssertEqual(
                    rendered.runs.compactMap { $0.link?.absoluteString }.sorted(),
                    ConsentLink.allCases.map(\.url.absoluteString).sorted(),
                    "\(catalog) \(key) [\(locale)] did not resolve to both web pages"
                )
            }
        }
    }

    func testConsentSentencesCarryNoLiteralDomain() throws {
        for (catalog, key) in Self.consentKeys {
            for locale in Self.locales {
                let sentence = try value(catalog: catalog, key: key, locale: locale)
                XCTAssertFalse(
                    sentence.contains(CleansiaWeb.domain),
                    "\(catalog) \(key) [\(locale)] spells the domain out — translators carry placeholders only"
                )
            }
        }
    }
}
