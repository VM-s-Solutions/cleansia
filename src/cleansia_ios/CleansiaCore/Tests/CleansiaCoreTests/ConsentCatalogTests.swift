import Foundation
import XCTest
@testable import CleansiaCore

/// A legal sentence renders as plain text when its markup is missing, so a
/// translation that dropped a link would silently unpublish a legal document
/// instead of failing. Pin the markup in every locale of both apps — the two
/// consent ticks and the booking wizard's contract-for-work line, each against
/// the pages it names.
final class ConsentCatalogTests: XCTestCase {
    private struct LegalSentence {
        let catalog: String
        let key: String
        let links: [ConsentLink]
    }

    private static let customerCatalog = "CleansiaCustomer/Resources/Localizable.xcstrings"
    private static let partnerCatalog = "CleansiaPartner/Resources/Localizable.xcstrings"

    private static let sentences = [
        LegalSentence(catalog: customerCatalog, key: "register_terms_and_conditions", links: [.terms, .privacy]),
        LegalSentence(catalog: partnerCatalog, key: "accept_terms", links: [.terms, .privacy]),
        LegalSentence(catalog: customerCatalog, key: "booking_work_contract_notice", links: [.workContract])
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

    func testEveryLegalSentenceLinksThePagesItNamesInEveryLocale() throws {
        for sentence in Self.sentences {
            for locale in Self.locales {
                let text = try value(catalog: sentence.catalog, key: sentence.key, locale: locale)
                for link in sentence.links {
                    XCTAssertTrue(
                        text.contains("(\(link.rawValue))"),
                        "\(sentence.catalog) \(sentence.key) [\(locale)] is missing the \(link.rawValue) link: \(text)"
                    )
                }
                let rendered = ConsentMarkdown.attributed(text)
                XCTAssertEqual(
                    rendered.runs.compactMap { $0.link?.absoluteString }.sorted(),
                    sentence.links.map(\.url.absoluteString).sorted(),
                    "\(sentence.catalog) \(sentence.key) [\(locale)] did not resolve to exactly the pages it names"
                )
            }
        }
    }

    /// A target no sentence carries is a page the apps never link to — either the sentence that
    /// should name it is missing from this roster, or the case is dead.
    func testEveryLinkTargetIsNamedByAtLeastOnePinnedSentence() {
        let pinned = Set(Self.sentences.flatMap(\.links))
        for link in ConsentLink.allCases {
            XCTAssertTrue(pinned.contains(link), "\(link.rawValue) is a target no pinned sentence carries")
        }
    }

    func testLegalSentencesCarryNoLiteralDomain() throws {
        for sentence in Self.sentences {
            for locale in Self.locales {
                let text = try value(catalog: sentence.catalog, key: sentence.key, locale: locale)
                XCTAssertFalse(
                    text.contains(CleansiaWeb.domain),
                    "\(sentence.catalog) \(sentence.key) [\(locale)] spells the domain out"
                        + " — translators carry placeholders only"
                )
            }
        }
    }
}
