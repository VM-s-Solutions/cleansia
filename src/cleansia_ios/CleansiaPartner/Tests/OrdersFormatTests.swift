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
}

private extension Character {
    var isCyrillic: Bool {
        unicodeScalars.allSatisfy { $0.value >= 0x0400 && $0.value <= 0x04FF }
    }
}
