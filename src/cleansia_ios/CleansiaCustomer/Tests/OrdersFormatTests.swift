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

    func testDateTimeLocalizesPerAppLocale() {
        XCTAssertNotEqual(
            OrdersFormat.dateTime(instant, locale: ruLocale),
            OrdersFormat.dateTime(instant, locale: enLocale)
        )
    }

    func testServicesSummaryUsesTranslationForAppLocale() {
        let order = OrderListItem(
            selectedPackages: [],
            selectedServices: [
                ServiceListItem(name: "Deep Clean", translations: ["ru": Translation(name: "Глубокая уборка")])
            ]
        )

        XCTAssertEqual(OrdersFormat.servicesSummary(order, locale: ruLocale), "Глубокая уборка")
        XCTAssertEqual(OrdersFormat.servicesSummary(order, locale: enLocale), "Deep Clean")
    }

    func testServicesSummaryFallsBackToFrozenNameWhenLocaleMissing() {
        let order = OrderListItem(
            selectedPackages: [
                PackageListItem(name: "Move-Out", translations: ["cs": Translation(name: "Vystěhování")])
            ],
            selectedServices: []
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
