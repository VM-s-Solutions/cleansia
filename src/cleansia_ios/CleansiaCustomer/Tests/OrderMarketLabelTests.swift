import CleansiaCore
import Foundation
import XCTest
@testable import CleansiaCustomer

/// An order is labelled with the market it was booked in — the country by name and the currency it was
/// priced in — resolved off the order's own `countryId` against the directory, never off the market the
/// customer happens to be browsing now.
final class OrderMarketLabelTests: XCTestCase {
    private let directory = MarketFixtures.two
    private let english = Locale(identifier: "en")
    private let czech = Locale(identifier: "cs")
    private let languages = ["en", "cs", "sk", "uk", "ru"]
    private let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main

    // MARK: The resolver

    func testHistoricalMarketsRemainIndependentOfTheBrowsingSelection() {
        let browsingCz = MarketState.resolved(selected: MarketFixtures.czechia, markets: directory)
        let browsingSk = MarketState.resolved(selected: MarketFixtures.slovakia, markets: directory)

        XCTAssertEqual(OrderMarketLabel.marketName(countryId: "svk", markets: browsingCz, locale: english), "Slovakia")
        XCTAssertEqual(OrderMarketLabel.marketName(countryId: "svk", markets: browsingSk, locale: english), "Slovakia")
        XCTAssertEqual(OrderMarketLabel.marketName(countryId: "cze", markets: browsingSk, locale: english), "Czechia")
        XCTAssertEqual(OrderMarketLabel.marketName(countryId: "cze", markets: browsingSk, locale: czech), "Česko")
    }

    func testMissingCountriesNeverBorrowTheSelectedMarket() {
        let markets = MarketState.resolved(selected: MarketFixtures.czechia, markets: directory)

        XCTAssertNil(OrderMarketLabel.marketName(countryId: "retired", markets: markets, locale: english))
        XCTAssertNil(OrderMarketLabel.marketName(countryId: nil, markets: markets, locale: english))
        XCTAssertNil(OrderMarketLabel.marketName(countryId: "cze", markets: .unavailable, locale: english))
        XCTAssertNil(OrderMarketLabel.marketName(countryId: "cze", markets: .loading, locale: english))
    }

    func testABlankTranslationFallsBackToTheDirectoryName() {
        let germany = MarketFixtures.market(
            countryId: "deu",
            isoCode: "DEU",
            isoAlpha2: "DE",
            name: "Germany",
            currencyCode: "EUR",
            translations: ["cs": CatalogTranslation(name: "", description: nil)]
        )
        let markets = MarketState.resolved(selected: germany, markets: [germany])

        XCTAssertEqual(OrderMarketLabel.marketName(countryId: "deu", markets: markets, locale: czech), "Germany")
    }

    // MARK: The label

    func testTheLabelPairsTheCountryWithTheOrdersOwnCurrency() {
        let markets = MarketState.resolved(selected: MarketFixtures.czechia, markets: directory)

        XCTAssertEqual(
            OrderMarketLabel.text(countryId: "svk", currencyCode: "EUR", markets: markets, locale: english),
            L10n.Orders.marketLabel("Slovakia", "EUR")
        )
    }

    func testAnUnlistedCountryReadsAsUnavailableAndABlankCurrencyAsADash() {
        let markets = MarketState.resolved(selected: MarketFixtures.czechia, markets: directory)
        let unavailable = L10n.Orders.marketLabel(L10n.Orders.marketUnknown, "—")

        XCTAssertEqual(
            OrderMarketLabel.text(countryId: "retired", currencyCode: " ", markets: markets, locale: english),
            unavailable
        )
        XCTAssertEqual(
            OrderMarketLabel.text(countryId: nil, currencyCode: nil, markets: .unavailable, locale: english),
            unavailable
        )
    }

    // MARK: The call sites

    /// Both surfaces read each order's OWN country and currency and observe the directory the view
    /// model exposes — a card that printed the browsing market would relabel every past booking on a
    /// market switch.
    func testTheListCardAndTheDetailLabelEachOrderOffItsOwnFields() throws {
        for file in ["OrdersTab.swift", "OrderDetailContent.swift"] {
            let source = try read("CleansiaCustomer/Sources/Features/Orders/\(file)")
            XCTAssertTrue(source.contains("OrderMarketLabel.text("), "\(file) renders no market label")
            XCTAssertTrue(source.contains("countryId: order.countryId"), "\(file) ignores the order's country")
            XCTAssertTrue(source.contains("currencyCode: order.currencyCode"), "\(file) ignores the order's currency")
        }
        for file in ["OrdersListViewModel.swift", "OrderDetailViewModel.swift"] {
            let source = try read("CleansiaCustomer/Sources/Features/Orders/\(file)")
            XCTAssertTrue(
                source.contains("marketStore.$state.assign(to: &$markets)"),
                "\(file) does not observe the directory"
            )
        }
    }

    // MARK: The catalog

    func testTheLabelAndTheUnavailableStateArePresentInEveryLocale() throws {
        let englishTable = try localizableTable(for: "en")
        for language in languages {
            let table = try localizableTable(for: language)
            XCTAssertEqual(table["order_market_label"], "%1$@ · %2$@", "\(language).lproj reshaped the label")
            let unknown = try XCTUnwrap(table["order_market_unknown"], "order_market_unknown missing from \(language)")
            XCTAssertFalse(unknown.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            if language != "en" {
                XCTAssertNotEqual(unknown, englishTable["order_market_unknown"], "\(language).lproj left it in English")
            }
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

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
