import Foundation
import XCTest
@testable import CleansiaCore

final class ConsentMarkdownTests: XCTestCase {
    private func links(_ attributed: AttributedString) -> [(text: String, url: String?)] {
        attributed.runs.map { run in
            (String(attributed[run.range].characters), run.link?.absoluteString)
        }
    }

    private func linkedTargets(_ attributed: AttributedString) -> [String] {
        attributed.runs.compactMap { $0.link?.absoluteString }
    }

    func testPlaceholderTargetsAreRewrittenToTheWebUrls() {
        let attributed = ConsentMarkdown.attributed(
            "I agree to the [Terms of Service](cleansia://terms) and [Privacy Policy](cleansia://privacy)"
        )

        XCTAssertEqual(String(attributed.characters), "I agree to the Terms of Service and Privacy Policy")
        XCTAssertEqual(
            linkedTargets(attributed),
            ["\(CleansiaWeb.origin)/terms", "\(CleansiaWeb.origin)/privacy"]
        )
        XCTAssertTrue(linkedTargets(attributed).allSatisfy { $0.hasPrefix("https://") })
    }

    func testLinkedRunsCarryOnlyTheLinkedWords() {
        let attributed = ConsentMarkdown.attributed("I agree to the [Terms of Service](cleansia://terms)")
        let linked = links(attributed).filter { $0.url != nil }

        XCTAssertEqual(linked.count, 1)
        XCTAssertEqual(linked.first?.text, "Terms of Service")
    }

    func testTranslatedSentenceKeepsItsMarkupAndTargets() {
        let attributed = ConsentMarkdown.attributed(
            "Súhlasím s [Podmienkami služby](cleansia://terms) a [Zásadami ochrany súkromia](cleansia://privacy)"
        )

        XCTAssertEqual(
            String(attributed.characters),
            "Súhlasím s Podmienkami služby a Zásadami ochrany súkromia"
        )
        XCTAssertEqual(
            linkedTargets(attributed),
            [ConsentLink.terms.url.absoluteString, ConsentLink.privacy.url.absoluteString]
        )
    }

    func testUnknownPlaceholderTargetIsStrippedRatherThanOpened() {
        let attributed = ConsentMarkdown.attributed("I agree to the [Terms](cleansia://something-else)")

        XCTAssertEqual(String(attributed.characters), "I agree to the Terms")
        XCTAssertTrue(linkedTargets(attributed).isEmpty)
    }

    func testTranslationThatDroppedTheMarkupStillRendersTheSentence() {
        let sentence = "Я соглашаюсь с Условиями использования и Политикой конфиденциальности"
        let attributed = ConsentMarkdown.attributed(sentence)

        XCTAssertEqual(String(attributed.characters), sentence)
        XCTAssertTrue(linkedTargets(attributed).isEmpty)
    }

    func testMalformedMarkupFallsBackToPlainText() {
        let sentence = "I agree to the [Terms of Service](cleansia://terms and the *Privacy Policy"
        let attributed = ConsentMarkdown.attributed(sentence)

        XCTAssertEqual(String(attributed.characters), sentence)
        XCTAssertTrue(linkedTargets(attributed).isEmpty)
    }

    func testExplicitTargetsOverrideTheDefaults() throws {
        let replacement = try XCTUnwrap(URL(string: "https://example.test/tos"))
        let attributed = ConsentMarkdown.attributed(
            "See the [Terms](cleansia://terms)",
            targets: ["cleansia://terms": replacement]
        )

        XCTAssertEqual(linkedTargets(attributed), ["https://example.test/tos"])
    }
}

final class CleansiaWebTests: XCTestCase {
    func testLegalUrlsDeriveFromTheSingleOrigin() {
        XCTAssertEqual(CleansiaWeb.origin, "https://\(CleansiaWeb.domain)")
        XCTAssertEqual(CleansiaWeb.termsURL.absoluteString, "\(CleansiaWeb.origin)/terms")
        XCTAssertEqual(CleansiaWeb.privacyURL.absoluteString, "\(CleansiaWeb.origin)/privacy")
    }

    func testReferralLinkAndSupportEmailShareTheSameDomain() {
        XCTAssertEqual(CleansiaWeb.referralLink(code: "ABC123"), "\(CleansiaWeb.origin)/r/ABC123")
        XCTAssertEqual(CleansiaWeb.supportEmail, "support@\(CleansiaWeb.domain)")
    }
}
