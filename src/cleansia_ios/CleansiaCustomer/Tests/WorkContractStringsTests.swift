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

    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

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

    /// The server stores only the version of the tick, so an iOS order is evidence of agreeing to the text
    /// the web catalog files under that version, word for word.
    func testTheEarlyPerformanceRequestIsTheTextFiledUnderTheVersionTheServerRecords() throws {
        let version = try earlyPerformanceVersionInForce()
        for language in languages {
            L10n.bundle = try localeBundle(language)
            let web = try webEarlyPerformanceText(version: version, language: language)
            XCTAssertEqual(L10n.Booking.earlyPerformanceRequest, web, "\(language) shows another text under \(version)")
        }
    }

    func testTheContractNoticeIsTheSentenceTheAndroidAppShows() throws {
        for language in languages {
            L10n.bundle = try localeBundle(language)
            let android = try androidContractNotice(language: language)
            XCTAssertEqual(L10n.Booking.contractNotice, android, "\(language) tells the customer another counterparty")
        }
    }

    private func sourceRoot() -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }

    private func earlyPerformanceVersionInForce() throws -> String {
        let order = try String(
            contentsOf: sourceRoot().appendingPathComponent("Cleansia.Core.Domain/Orders/Order.cs"),
            encoding: .utf8
        )
        let regex = try NSRegularExpression(pattern: #"EarlyPerformanceConsentTextVersionInForce\s*=\s*"([^"]+)""#)
        let match = try XCTUnwrap(
            regex.firstMatch(in: order, range: NSRange(order.startIndex..., in: order)),
            "Order.EarlyPerformanceConsentTextVersionInForce not found — the parser needs updating"
        )
        let version = try XCTUnwrap(Range(match.range(at: 1), in: order))
        return String(order[version])
    }

    private func webEarlyPerformanceText(version: String, language: String) throws -> String {
        let url = sourceRoot().appendingPathComponent("Cleansia.App/apps/cleansia.app/src/assets/i18n/\(language).json")
        let web = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any])
        let order = (web["pages"] as? [String: Any])?["order"] as? [String: Any]
        let texts = order?["early_performance"] as? [String: Any]
        return try XCTUnwrap(texts?[version] as? String, "the web \(language) catalog files no text under \(version)")
    }

    /// Android marks the terms link as HTML; the same link in the catalog's markdown is `[label](target)`.
    private func androidContractNotice(language: String) throws -> String {
        let folder = language == "en" ? "values" : "values-\(language)"
        let url = sourceRoot()
            .appendingPathComponent("cleansia_android/customer-app/src/main/res/\(folder)/strings.xml")
        let xml = try String(contentsOf: url, encoding: .utf8)
        let entry = try NSRegularExpression(
            pattern: #"<string name="booking_contract_notice"><!\[CDATA\[(.*?)\]\]></string>"#,
            options: [.dotMatchesLineSeparators]
        )
        let match = try XCTUnwrap(
            entry.firstMatch(in: xml, range: NSRange(xml.startIndex..., in: xml)),
            "booking_contract_notice not found in the Android \(folder) strings"
        )
        let cdata = try XCTUnwrap(Range(match.range(at: 1), in: xml))
        let html = String(xml[cdata])
        let anchor = try NSRegularExpression(pattern: #"<a href="([^"]+)">([^<]+)</a>"#)
        return anchor.stringByReplacingMatches(
            in: html,
            range: NSRange(html.startIndex..., in: html),
            withTemplate: "[$2]($1)"
        )
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
