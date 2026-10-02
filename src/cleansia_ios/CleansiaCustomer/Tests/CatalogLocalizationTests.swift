import Foundation
import XCTest
@testable import CleansiaCustomer

final class CatalogLocalizationTests: XCTestCase {
    private let russian = Locale(identifier: "ru")
    private let english = Locale(identifier: "en")
    private let slovak = Locale(identifier: "sk")

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

        XCTAssertEqual(service.localizedName(for: russian), "Глубокая уборка")
        XCTAssertEqual(service.localizedName(for: english), "Deep cleaning")
        XCTAssertEqual(service.localizedName(for: slovak), "Deep cleaning")
        XCTAssertEqual(service.localizedDescription(for: russian), "Тщательная уборка")
        XCTAssertEqual(service.localizedDescription(for: english), "Thorough clean")
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

        XCTAssertEqual(package.localizedName(for: russian), "Уборка при переезде")
        XCTAssertEqual(package.localizedName(for: english), "Move-out")
        XCTAssertEqual(package.includesSummary(for: russian), "\(L10n.Booking.packageIncludesPrefix) Окна")
        XCTAssertEqual(package.includesSummary(for: english), "\(L10n.Booking.packageIncludesPrefix) Windows")
    }

    func testCategoryNameUsesTheAppLanguage() {
        XCTAssertEqual(category.localizedName(for: russian), "Дом")
        XCTAssertEqual(category.localizedName(for: english), "Home")
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

        XCTAssertEqual(extra.localizedName(for: russian), "Окна")
        XCTAssertEqual(extra.localizedName(for: english), "Windows")
        XCTAssertEqual(extra.localizedDescription(for: russian), "Внутреннее стекло")
        XCTAssertEqual(extra.localizedDescription(for: english), "Inside glass")
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
