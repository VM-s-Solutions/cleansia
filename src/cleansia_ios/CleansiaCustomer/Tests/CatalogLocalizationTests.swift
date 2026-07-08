import Foundation
import XCTest
@testable import CleansiaCustomer

final class CatalogLocalizationTests: XCTestCase {
    private let ru = Locale(identifier: "ru")
    private let en = Locale(identifier: "en")
    private let sk = Locale(identifier: "sk")

    func testServiceNameAndDescriptionSelectTheAppLanguageTranslation() {
        let service = CatalogService(
            id: "s1",
            name: "Deep cleaning",
            description: "Thorough clean",
            basePrice: 900,
            perRoomPrice: 150,
            category: category,
            translations: ["ru": CatalogTranslation(name: "Глубокая уборка", description: "Тщательная уборка")]
        )

        XCTAssertEqual(service.localizedName(for: ru), "Глубокая уборка")
        XCTAssertEqual(service.localizedName(for: en), "Deep cleaning")
        XCTAssertEqual(service.localizedName(for: sk), "Deep cleaning")
        XCTAssertEqual(service.localizedDescription(for: ru), "Тщательная уборка")
        XCTAssertEqual(service.localizedDescription(for: en), "Thorough clean")
    }

    func testPackageNameAndIncludesSummaryUseTheAppLanguage() {
        let package = CatalogPackage(
            id: "p1",
            name: "Move-out",
            description: "Top to bottom",
            price: 2500,
            translations: ["ru": CatalogTranslation(name: "Уборка при переезде", description: nil)],
            includedServices: [
                CatalogPackageServiceSummary(
                    name: "Windows",
                    translations: ["ru": CatalogTranslation(name: "Окна", description: nil)]
                )
            ]
        )

        XCTAssertEqual(package.localizedName(for: ru), "Уборка при переезде")
        XCTAssertEqual(package.localizedName(for: en), "Move-out")
        XCTAssertEqual(package.includesSummary(for: ru), "\(L10n.Booking.packageIncludesPrefix) Окна")
        XCTAssertEqual(package.includesSummary(for: en), "\(L10n.Booking.packageIncludesPrefix) Windows")
    }

    func testCategoryNameUsesTheAppLanguage() {
        XCTAssertEqual(category.localizedName(for: ru), "Дом")
        XCTAssertEqual(category.localizedName(for: en), "Home")
    }

    func testExtraNameAndDescriptionUseTheAppLanguage() {
        let extra = CatalogExtra(
            id: "e1",
            slug: "windows",
            name: "Windows",
            description: "Inside glass",
            price: 200,
            displayOrder: 0,
            translations: ["ru": CatalogTranslation(name: "Окна", description: "Внутреннее стекло")]
        )

        XCTAssertEqual(extra.localizedName(for: ru), "Окна")
        XCTAssertEqual(extra.localizedName(for: en), "Windows")
        XCTAssertEqual(extra.localizedDescription(for: ru), "Внутреннее стекло")
        XCTAssertEqual(extra.localizedDescription(for: en), "Inside glass")
    }

    private var category: CatalogCategory {
        CatalogCategory(
            id: "c-home",
            slug: "home",
            name: "Home",
            description: nil,
            displayOrder: 0,
            translations: ["ru": CatalogTranslation(name: "Дом", description: nil)]
        )
    }
}
