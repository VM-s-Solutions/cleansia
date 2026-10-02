import CleansiaCustomerApi
import Foundation
import XCTest
@testable import CleansiaCustomer

final class OrdersFormatTests: XCTestCase {
    private let instant = Date(timeIntervalSince1970: 1_720_425_600)
    private let ruLocale = Locale(identifier: "ru")
    private let enLocale = Locale(identifier: "en")

    func testDateRangeLocalizesWeekdayAndMonthPerAppLocale() {
        let russian = OrdersFormat.dateRange(instant, estimatedMinutes: 120, locale: ruLocale)
        let english = OrdersFormat.dateRange(instant, estimatedMinutes: 120, locale: enLocale)

        XCTAssertNotEqual(russian, english)
        XCTAssertNotEqual(english, "—")
        XCTAssertTrue(russian.contains { $0.isCyrillic })
    }

    /// A missing code used to render as "Kč" — a label guessed for a figure the payload never labelled.
    func testABlankCurrencyCodeRendersNoUnit() {
        let labelled = OrdersFormat.price(1290, currencyCode: "CZK")
        XCTAssertTrue(labelled.hasSuffix(" Kč"))
        let bare = String(labelled.dropLast(" Kč".count))
        XCTAssertEqual(OrdersFormat.price(1290, currencyCode: nil), bare)
        XCTAssertEqual(OrdersFormat.price(1290, currencyCode: ""), bare)
        XCTAssertEqual(OrdersFormat.price(1290, currencyCode: "  "), bare)
        XCTAssertTrue(OrdersFormat.price(1290, currencyCode: "EUR").hasSuffix(" €"))
    }

    /// A credit share can leave haléře; rounding them away shows a figure the card is not charged.
    func testAnAmountThatIsNotWholeShowsItsMinorUnits() {
        let american = Locale(identifier: "en_US")
        XCTAssertEqual(OrdersFormat.price(319.90, currencyCode: "CZK", locale: american), "319.90 Kč")
        XCTAssertEqual(OrdersFormat.price(137.10, currencyCode: "CZK", locale: american), "137.10 Kč")
        XCTAssertEqual(OrdersFormat.price(12.5, currencyCode: "EUR", locale: american), "12.50 €")
        XCTAssertEqual(OrdersFormat.price(1200.05, currencyCode: "USD", locale: american), "$1,200.05")
        XCTAssertEqual(OrdersFormat.price(-57.6, currencyCode: "CZK", locale: american), "-57.60 Kč")
    }

    func testAWholeAmountStaysWholeWithinHalfAMinorUnit() {
        let american = Locale(identifier: "en_US")
        XCTAssertEqual(OrdersFormat.price(320, currencyCode: "CZK", locale: american), "320 Kč")
        XCTAssertEqual(OrdersFormat.price(319.999, currencyCode: "CZK", locale: american), "320 Kč")
        XCTAssertEqual(OrdersFormat.price(1200, currencyCode: "CZK", locale: american), "1,200 Kč")
        XCTAssertEqual(OrdersFormat.price(0, currencyCode: "CZK", locale: american), "0 Kč")
        XCTAssertEqual(OrdersFormat.price(-0.001, currencyCode: "CZK", locale: american), "0 Kč")
    }

    /// The currency's own minor unit: none for yen; two for a blank or unknown code.
    func testTheFractionDigitsComeFromTheCurrency() {
        let american = Locale(identifier: "en_US")
        XCTAssertEqual(OrdersFormat.price(1199.6, currencyCode: "JPY", locale: american), "1,200 JPY")
        XCTAssertEqual(OrdersFormat.price(319.9, currencyCode: nil, locale: american), "319.90")
        XCTAssertEqual(OrdersFormat.price(319.9, currencyCode: "XYZ", locale: american), "319.90 XYZ")
    }

    func testTheLocalesDecimalMarkIsUsed() {
        XCTAssertEqual(OrdersFormat.price(319.9, currencyCode: "CZK", locale: Locale(identifier: "cs_CZ")), "319,90 Kč")
    }

    func testDateTimeLocalizesPerAppLocale() {
        XCTAssertNotEqual(
            OrdersFormat.dateTime(instant, locale: ruLocale),
            OrdersFormat.dateTime(instant, locale: enLocale)
        )
    }

    func testServicesSummaryUsesTranslationForAppLocale() {
        let order = OrderFixtures.summary(
            services: [
                CustomerOrderLineName(name: "Deep Clean", translations: ["ru": Translation(name: "Глубокая уборка")])
            ]
        )

        XCTAssertEqual(OrdersFormat.servicesSummary(order, locale: ruLocale), "Глубокая уборка")
        XCTAssertEqual(OrdersFormat.servicesSummary(order, locale: enLocale), "Deep Clean")
    }

    func testServicesSummaryFallsBackToFrozenNameWhenLocaleMissing() {
        let order = OrderFixtures.summary(
            packages: [
                CustomerOrderLineName(name: "Move-Out", translations: ["cs": Translation(name: "Vystěhování")])
            ]
        )

        XCTAssertEqual(OrdersFormat.servicesSummary(order, locale: ruLocale), "Move-Out")
    }

    func testLocalizedCatalogNameUsesDetailTranslationForAppLocale() {
        let service = ServiceDetails(name: "Deep Clean", translations: ["ru": Translation(name: "Глубокая уборка")])

        XCTAssertEqual(
            OrdersFormat.localizedCatalogName(service.name, translations: service.translations, locale: ruLocale),
            "Глубокая уборка"
        )
        XCTAssertEqual(
            OrdersFormat.localizedCatalogName(service.name, translations: service.translations, locale: enLocale),
            "Deep Clean"
        )
    }

    func testLocalizedCatalogNameFallsBackToFrozenSnapshotWhenLocaleMissing() {
        let package = PackageDetails(name: "Move-Out", translations: ["cs": Translation(name: "Vystěhování")])

        XCTAssertEqual(
            OrdersFormat.localizedCatalogName(package.name, translations: package.translations, locale: ruLocale),
            "Move-Out"
        )
    }

    func testLocalizedCatalogNameEmDashWhenNothingPresent() {
        XCTAssertEqual(OrdersFormat.localizedCatalogName(nil, translations: nil, locale: enLocale), "—")
    }

    func testLocalizedCatalogDescriptionPrefersTranslationThenFallback() {
        let service = ServiceDetails(
            description: "English desc",
            translations: ["ru": Translation(name: "Имя", description: "Русское описание")]
        )

        XCTAssertEqual(
            OrdersFormat.localizedCatalogDescription(
                service.description,
                translations: service.translations,
                locale: ruLocale
            ),
            "Русское описание"
        )
        XCTAssertEqual(
            OrdersFormat.localizedCatalogDescription(
                service.description,
                translations: service.translations,
                locale: enLocale
            ),
            "English desc"
        )
        XCTAssertNil(OrdersFormat.localizedCatalogDescription(nil, translations: nil, locale: enLocale))
    }
}

private extension Character {
    var isCyrillic: Bool {
        unicodeScalars.allSatisfy { $0.value >= 0x0400 && $0.value <= 0x04FF }
    }
}
