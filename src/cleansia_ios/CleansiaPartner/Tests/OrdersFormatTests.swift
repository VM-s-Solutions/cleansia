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
}

private extension Character {
    var isCyrillic: Bool {
        unicodeScalars.allSatisfy { $0.value >= 0x0400 && $0.value <= 0x04FF }
    }
}
