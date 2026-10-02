import CleansiaPartnerApi
import Foundation
import XCTest
@testable import CleansiaPartner

final class OrdersFormatTests: XCTestCase {
    private let ruLocale = Locale(identifier: "ru")
    private let enLocale = Locale(identifier: "en")
    private let instant = Date(timeIntervalSince1970: 1_623_758_400)

    func testRelativeDateTimeLocalizesWeekdayAndMonthPerAppLocale() {
        let russian = OrdersFormat.relativeDateTime(instant, locale: ruLocale)
        let english = OrdersFormat.relativeDateTime(instant, locale: enLocale)

        XCTAssertNotEqual(russian, english)
        XCTAssertNotEqual(english, "—")
        XCTAssertTrue(russian.contains { $0.isCyrillic })
    }

    func testDayHeaderLocalizesPerAppLocale() {
        XCTAssertNotEqual(
            OrdersFormat.dayHeader(instant, locale: ruLocale),
            OrdersFormat.dayHeader(instant, locale: enLocale)
        )
    }

    // MARK: money

    /// Pay is booked to the haléř and a seat's share of a job need not be whole, so 412.30 was "412 Kč" on
    /// the board while the card and Stripe said 412.30. The board, the offer, the contract and the detail
    /// all read this one formatter, so they now agree on the minor units.
    func testAPayThatIsNotWholeKeepsItsMinorUnitsInTheLocalesMark() {
        let czech = Locale(identifier: "cs_CZ")
        let american = Locale(identifier: "en_US")
        XCTAssertEqual(OrdersFormat.money(412.30, symbol: "Kč", locale: czech), "412,30 Kč")
        XCTAssertEqual(OrdersFormat.money(412.5, symbol: "€", locale: american), "412.50 €")
        XCTAssertEqual(OrdersFormat.money(1412.3, symbol: "Kč", locale: czech), "1\u{202F}412,30 Kč")
        XCTAssertEqual(OrdersFormat.money(12.05, symbol: nil, locale: czech), "12,05")
    }

    func testAWholePayStaysWholeWithinHalfAHaler() {
        let czech = Locale(identifier: "cs_CZ")
        XCTAssertEqual(OrdersFormat.money(412, symbol: "Kč", locale: czech), "412 Kč")
        XCTAssertEqual(OrdersFormat.money(1274.999, symbol: "Kč", locale: czech), "1\u{202F}275 Kč")
        XCTAssertEqual(OrdersFormat.money(-0.001, symbol: "Kč", locale: czech), "0 Kč")
    }

    func testTheBoardTotalKeepsTheMinorUnitsOfItsRows() {
        let total = OrdersFormat.totalEarnings([
            order(pay: 412.30, code: "CZK", symbol: "Kč"),
            order(pay: 500, code: "CZK", symbol: "Kč")
        ])
        XCTAssertEqual(total, OrdersFormat.money(912.30, symbol: "Kč"))
        XCTAssertEqual(
            OrdersFormat.pay(order(pay: 412.30, code: "CZK", symbol: "Kč")),
            OrdersFormat.money(412.30, symbol: "Kč")
        )
    }

    // MARK: totalEarnings

    private func order(pay: Double?, code: String?, symbol: String?) -> OrderListItem {
        var item = OrderListItem()
        item.estimatedCleanerPay = pay
        item.currency = code.map { CurrencyListItem(id: "cur-\($0)", code: $0, symbol: symbol, name: $0) }
        return item
    }

    func testASingleCurrencyBoardSumsIntoOneLabelledFigure() {
        let total = OrdersFormat.totalEarnings([
            order(pay: 1000, code: "CZK", symbol: "Kč"),
            order(pay: 275, code: "CZK", symbol: "Kč")
        ])

        XCTAssertEqual(total, OrdersFormat.money(1275, symbol: "Kč"))
    }

    /// The old form added CZK and EUR into one bare number. Two currencies are two figures, each with
    /// its own unit, in the order the board shows them.
    func testAMixedBoardIsOneFigurePerCurrencyNeverASumAcrossThem() {
        let total = OrdersFormat.totalEarnings([
            order(pay: 1000, code: "CZK", symbol: "Kč"),
            order(pay: 40, code: "EUR", symbol: "€"),
            order(pay: 275, code: "CZK", symbol: "Kč")
        ])

        XCTAssertEqual(total, "\(OrdersFormat.money(1275, symbol: "Kč")) · \(OrdersFormat.money(40, symbol: "€"))")
    }

    /// Grouping is by code: two rows in the same currency with the symbol spelled differently on the
    /// wire are still one figure.
    func testGroupingIsByCodeNotBySymbol() {
        let total = OrdersFormat.totalEarnings([
            order(pay: 1000, code: "CZK", symbol: "Kč"),
            order(pay: 275, code: "CZK", symbol: "CZK")
        ])

        XCTAssertEqual(total, OrdersFormat.money(1275, symbol: "Kč"))
    }

    func testOrdersWithoutACurrencyFormTheirOwnUnlabelledGroup() {
        let total = OrdersFormat.totalEarnings([
            order(pay: 500, code: nil, symbol: nil),
            order(pay: 40, code: "EUR", symbol: "€"),
            order(pay: 200, code: nil, symbol: nil)
        ])

        XCTAssertEqual(total, "\(OrdersFormat.money(700, symbol: nil)) · \(OrdersFormat.money(40, symbol: "€"))")
    }

    func testAnEmptyBoardIsAnUnlabelledZero() {
        XCTAssertEqual(OrdersFormat.totalEarnings([]), OrdersFormat.money(0, symbol: nil))
    }

    // MARK: the contract's window and instant

    func testTheWindowRunsFromTheStartToTheEstimatedEnd() {
        let window = OrdersFormat.window(instant, minutes: 180, locale: enLocale)

        let start = OrdersFormat.timeOnly(instant, locale: enLocale)
        let end = OrdersFormat.timeOnly(instant.addingTimeInterval(180 * 60), locale: enLocale)
        XCTAssertTrue(window.hasSuffix(" · \(start)–\(end)"), window)
        XCTAssertTrue(window.contains("Jun"), window)
    }

    func testAWindowWithNoEstimateIsTheStartAlone() {
        let window = OrdersFormat.window(instant, minutes: 0, locale: enLocale)

        XCTAssertTrue(window.hasSuffix(" · \(OrdersFormat.timeOnly(instant, locale: enLocale))"), window)
        XCTAssertFalse(window.contains("–"), window)
    }

    func testTheWindowLocalizesTheMonthPerAppLocale() {
        XCTAssertTrue(OrdersFormat.window(instant, minutes: 60, locale: ruLocale).contains { $0.isCyrillic })
    }

    func testTheInstantCarriesTheYearAndTheTime() {
        let rendered = OrdersFormat.dateTime(instant, locale: enLocale)

        XCTAssertTrue(rendered.contains("2021"), rendered)
        XCTAssertTrue(rendered.hasSuffix(" · \(OrdersFormat.timeOnly(instant, locale: enLocale))"), rendered)
    }

    /// Owner ruling 2026-10-02: a bathroom is "ванна кімната" in Ukrainian and "ванная" in Russian, never
    /// "ванна/ванны" (a bathtub). Each count is formatted in its language's locale, whose plural rules pick
    /// the form.
    func testUkrainianAndRussianCountBathroomsNotBathtubs() throws {
        let expected: [String: [Int: String]] = [
            "uk": [1: "1 ванна кімната", 2: "2 ванні кімнати", 4: "4 ванні кімнати", 5: "5 ванних кімнат"],
            "ru": [1: "1 ванная", 2: "2 ванные", 4: "4 ванные", 5: "5 ванных"]
        ]
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for (language, forms) in expected {
            L10n.bundle = try localeBundle(language)
            let format = L10n.localized("scope_baths")
            for (count, form) in forms {
                XCTAssertEqual(String(format: format, locale: Locale(identifier: language), count), form, language)
            }
        }
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}

private extension Character {
    var isCyrillic: Bool {
        unicodeScalars.allSatisfy { $0.value >= 0x0400 && $0.value <= 0x04FF }
    }
}
