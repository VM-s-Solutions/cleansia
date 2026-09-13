import CleansiaCore
import Foundation
import XCTest
@testable import CleansiaCustomer

/// The copy figures that used to be typed into the locale strings — the insurance ceiling and the
/// no-show credit — come from the market or are not stated at all. A locale string never carries a
/// money figure or a currency name of its own.
final class MarketCopyTests: XCTestCase {
    private static let locales = ["en", "cs", "sk", "uk", "ru"]
    private static let currencyWord = "CZK|Kč|EUR|€"

    // MARK: The renderers

    func testTheTrustBadgeStatesTheMarketsCeilingInItsCurrency() {
        let text = InsuranceCopy.trustBadge(MarketMoney(amount: 1_000_000, currencyCode: "CZK"))
        XCTAssertEqual(text, L10n.Booking.trustInsured(OrdersFormat.price(1_000_000, currencyCode: "CZK")))
        XCTAssertTrue(text.hasSuffix(" Kč"), text)
        XCTAssertTrue(text.contains("000"), "the ceiling's digits come from the market, not the string")
    }

    func testWithoutACeilingTheTrustBadgeClaimsInsuranceAndNoFigure() {
        let text = InsuranceCopy.trustBadge(nil)
        XCTAssertEqual(text, L10n.Booking.trustInsuredNoFigure)
        XCTAssertNil(text.rangeOfCharacter(from: .decimalDigits), text)
    }

    func testTheFaqAnswerFollowsTheSameRule() {
        let stated = InsuranceCopy.faqAnswer(MarketMoney(amount: 50000, currencyCode: "EUR"))
        XCTAssertEqual(stated, L10n.Help.faqA3(OrdersFormat.price(50000, currencyCode: "EUR")))
        XCTAssertTrue(stated.contains("€"), stated)

        let unstated = InsuranceCopy.faqAnswer(nil)
        XCTAssertEqual(unstated, L10n.Help.faqA3NoFigure)
        XCTAssertNil(unstated.rangeOfCharacter(from: .decimalDigits), unstated)
    }

    // MARK: The catalog

    func testTheNoFigureVariantsCarryNoDigitAndNoCurrencyWordInAnyLocale() throws {
        let strings = try customerStrings()
        for key in ["booking_trust_insured_no_figure", "help_faq_a3_no_figure"] {
            for locale in Self.locales {
                let value = try value(of: key, locale, in: strings)
                XCTAssertNil(value.rangeOfCharacter(from: .decimalDigits), "\(locale)/\(key) states a figure: \(value)")
                XCTAssertFalse(matches(Self.currencyWord, value), "\(locale)/\(key) names a currency: \(value)")
            }
        }
    }

    /// The push announces the credit as different news from a plain cancellation, and states no
    /// amount: the figure is on the credit screen, where it arrives with its currency.
    func testTheNoCleanerPushAnnouncesACreditWithoutAFigureInBothCatalogs() throws {
        for (app, strings) in try [("customer", customerStrings()), ("partner", partnerStrings())] {
            for locale in Self.locales {
                let value = try value(of: "push.order.no_cleaner_refunded.body", locale, in: strings)
                let withoutSlot = value.replacingOccurrences(of: "%1$@", with: "")
                XCTAssertTrue(value.contains("#%1$@"), "\(app)/\(locale) lost the order-number slot: \(value)")
                XCTAssertNil(
                    withoutSlot.rangeOfCharacter(from: .decimalDigits),
                    "\(app)/\(locale) states a figure: \(value)"
                )
                XCTAssertFalse(matches(Self.currencyWord, value), "\(app)/\(locale) names a currency: \(value)")
            }
        }
    }

    func testTheCustomerCatalogNamesNoCurrencyAnywhere() throws {
        let strings = try customerStrings()
        var offenders: [String] = []
        for (key, entry) in strings {
            guard let localizations = (entry as? [String: Any])?["localizations"] as? [String: Any] else { continue }
            for locale in Self.locales {
                let unit = (localizations[locale] as? [String: Any])?["stringUnit"] as? [String: Any]
                if let value = unit?["value"] as? String, matches(Self.currencyWord, value) {
                    offenders.append("\(locale)/\(key)")
                }
            }
        }
        XCTAssertEqual(offenders, [], "money is formatted on the device from a number and a code")
    }

    func testTheSeasonalCardIsGone() throws {
        let strings = try customerStrings()
        XCTAssertNil(strings["home_seasonal_title"])
        XCTAssertNil(strings["home_seasonal_subtitle"])
    }

    // MARK: Helpers

    private func value(of key: String, _ locale: String, in strings: [String: Any]) throws -> String {
        let entry = try XCTUnwrap(strings[key] as? [String: Any], "\(key) missing from the catalog")
        let localizations = try XCTUnwrap(entry["localizations"] as? [String: Any])
        let unit = (localizations[locale] as? [String: Any])?["stringUnit"] as? [String: Any]
        return try XCTUnwrap(unit?["value"] as? String, "\(locale) lost \(key)")
    }

    private func customerStrings() throws -> [String: Any] {
        try strings(at: "CleansiaCustomer/Resources/Localizable.xcstrings")
    }

    private func partnerStrings() throws -> [String: Any] {
        try strings(at: "CleansiaPartner/Resources/Localizable.xcstrings")
    }

    private func strings(at relativePath: String) throws -> [String: Any] {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent(relativePath)
        let catalog = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any])
        return try XCTUnwrap(catalog["strings"] as? [String: Any])
    }

    private func matches(_ pattern: String, _ text: String) -> Bool {
        text.range(of: pattern, options: .regularExpression) != nil
    }
}
