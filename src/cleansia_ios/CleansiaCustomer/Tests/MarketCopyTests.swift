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
    private static let vettingClaim = "background|vetted|prověřen|preveren|перевірен|проверен"
    private static let insuranceClaim = "insur|pojišt|poist|застрах"

    // MARK: The renderers

    func testTheTrustBadgeStatesTheMarketsCeilingInItsCurrency() throws {
        let text = try XCTUnwrap(InsuranceCopy.trustBadge(MarketMoney(amount: 1_000_000, currencyCode: "CZK")))
        XCTAssertEqual(text, L10n.Booking.trustInsured(OrdersFormat.price(1_000_000, currencyCode: "CZK")))
        XCTAssertTrue(text.hasSuffix(" Kč"), text)
        XCTAssertTrue(text.contains("000"), "the ceiling's digits come from the market, not the string")
    }

    /// A cleaner needs no insurance to be approved, so without a ceiling the market stands behind
    /// nothing claims it.
    func testWithoutACeilingNothingClaimsInsurance() {
        XCTAssertNil(InsuranceCopy.trustBadge(nil))
        XCTAssertNil(InsuranceCopy.faqAnswer(nil))
    }

    func testTheFaqAnswerStatesTheMarketsCeilingInItsCurrency() throws {
        let stated = try XCTUnwrap(InsuranceCopy.faqAnswer(MarketMoney(amount: 50000, currencyCode: "EUR")))
        XCTAssertEqual(stated, L10n.Help.faqA3(OrdersFormat.price(50000, currencyCode: "EUR")))
        XCTAssertTrue(stated.contains("€"), stated)
    }

    // MARK: The catalog

    /// The only insurance copy left carries the market's figure, or is the question that figure answers.
    func testNoLocaleClaimsInsuranceWithoutTheMarketsFigure() throws {
        let strings = try customerStrings()
        var offenders: [String] = []
        for (key, entry) in strings where key != "help_faq_q3" {
            guard let localizations = (entry as? [String: Any])?["localizations"] as? [String: Any] else { continue }
            for locale in Self.locales {
                let unit = (localizations[locale] as? [String: Any])?["stringUnit"] as? [String: Any]
                if let value = unit?["value"] as? String,
                   matches(Self.insuranceClaim, value.lowercased()),
                   !value.contains("%1$@")
                {
                    offenders.append("\(locale)/\(key)")
                }
            }
        }
        XCTAssertEqual(offenders.sorted(), [], "insurance is claimed with no ceiling behind it")
    }

    /// The push announces the credit as different news from a plain cancellation. The credit
    /// arrives as the second loc-arg, formatted by the server in its own currency, so the copy takes
    /// it as a slot and states no figure and no currency of its own.
    func testTheNoCleanerPushTakesTheOrderNumberAndTheCreditAsSlotsInBothCatalogs() throws {
        let bodies = [
            "push.order.no_cleaner_refunded.body",
            "push.order.no_cleaner_refund_pending.body",
            "push.order.no_cleaner_nothing_charged.body"
        ]
        for (app, strings) in try [("customer", customerStrings()), ("partner", partnerStrings())] {
            for key in bodies {
                for locale in Self.locales {
                    let value = try value(of: key, locale, in: strings)
                    let withoutSlots = value
                        .replacingOccurrences(of: "%1$@", with: "")
                        .replacingOccurrences(of: "%2$@", with: "")
                    let site = "\(app)/\(locale)/\(key)"
                    XCTAssertTrue(value.contains("#%1$@"), "\(site) lost the order-number slot: \(value)")
                    XCTAssertTrue(value.contains("%2$@"), "\(site) lost the credit slot: \(value)")
                    XCTAssertNil(
                        withoutSlots.rangeOfCharacter(from: .decimalDigits),
                        "\(site) states a figure: \(value)"
                    )
                    XCTAssertFalse(matches(Self.currencyWord, value), "\(site) names a currency: \(value)")
                }
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

    /// No background check exists, so no locale promises one.
    func testNoLocalePromisesVettedCleaners() throws {
        let strings = try customerStrings()
        var offenders: [String] = []
        for (key, entry) in strings {
            guard let localizations = (entry as? [String: Any])?["localizations"] as? [String: Any] else { continue }
            for locale in Self.locales {
                let unit = (localizations[locale] as? [String: Any])?["stringUnit"] as? [String: Any]
                if let value = unit?["value"] as? String, matches(Self.vettingClaim, value.lowercased()) {
                    offenders.append("\(locale)/\(key)")
                }
            }
        }
        XCTAssertEqual(offenders, [], "the app promises a check nobody runs")
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
