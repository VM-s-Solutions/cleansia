import Foundation
import XCTest
@testable import CleansiaCustomer

final class ProfileStatsFormatTests: XCTestCase {
    private let feb2025 = Date(timeIntervalSince1970: 1_739_534_400)

    func testSavedMapsKnownCurrencyCodesToSymbols() {
        XCTAssertEqual(ProfileStatsFormat.saved(320, currencyCode: "CZK"), "320 Kč")
        XCTAssertEqual(ProfileStatsFormat.saved(45, currencyCode: "EUR"), "45 €")
        XCTAssertEqual(ProfileStatsFormat.saved(10, currencyCode: "USD"), "10 $")
    }

    func testSavedPassesAnUnknownCurrencyCodeThrough() {
        XCTAssertEqual(ProfileStatsFormat.saved(5, currencyCode: "PLN"), "5 PLN")
    }

    func testSavedOmitsTheSymbolWhenTheUserHasNoRealizedCurrency() {
        XCTAssertEqual(ProfileStatsFormat.saved(0, currencyCode: nil), "0")
    }

    func testMemberSinceRendersAbbreviatedMonthPlusYearInEnglish() {
        XCTAssertEqual(
            ProfileStatsFormat.memberSince(feb2025, locale: Locale(identifier: "en_US")),
            "Feb 2025"
        )
    }

    func testMemberSinceLocalizesTheMonthForCzech() {
        let formatted = ProfileStatsFormat.memberSince(feb2025, locale: Locale(identifier: "cs"))

        XCTAssertTrue(formatted.contains("2025"))
        XCTAssertNotEqual(formatted, "Feb 2025")
    }

    func testMemberSinceFallsBackToAnEmDashWhenUnknown() {
        XCTAssertEqual(ProfileStatsFormat.memberSince(nil, locale: Locale(identifier: "en_US")), "—")
    }

    /// The card's inputs are numbers a language switch rarely changes, so SwiftUI skipped its body and the
    /// three labels stayed in the old language while the rest of Profile switched. It is rebuilt on the
    /// locale's identity, the app's pattern.
    func testTheStatsCardIsRebuiltOnALanguageSwitch() throws {
        let tab = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/Features/Profile/ProfileTab.swift")
        let source = try String(contentsOf: tab, encoding: .utf8)
            .components(separatedBy: .newlines)
            .filter { !$0.trimmingCharacters(in: .whitespaces).hasPrefix("//") }
            .joined()
            .components(separatedBy: .whitespaces).joined()
        XCTAssertTrue(
            source
                .contains(
                    "memberSince:ProfileStatsFormat.memberSince(user?.memberSince,locale:locale)).id(locale.identifier)"
                ),
            "the stats card is no longer rebuilt on a language switch"
        )
    }
}
