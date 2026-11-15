import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class ScopeTranslationTests: XCTestCase {
    private let translations: [String: Translation] = [
        "cs": Translation(name: "Hloubkové čištění", description: "Popis"),
        "sk": Translation(name: "Hĺbkové čistenie"),
        "ru": Translation(name: nil),
        "pl": Translation(name: "  ")
    ]

    func testResolvesTranslatedNameForCurrentLocale() {
        XCTAssertEqual(
            ScopeTranslation.resolveTranslatedName(translations, lang: "cs", fallback: "Deep Cleaning"),
            "Hloubkové čištění"
        )
    }

    func testFallsBackToStoredNameWhenLocaleMissing() {
        XCTAssertEqual(
            ScopeTranslation.resolveTranslatedName(translations, lang: "uk", fallback: "Deep Cleaning"),
            "Deep Cleaning"
        )
    }

    func testFallsBackToStoredNameWhenTranslationNameIsNil() {
        XCTAssertEqual(
            ScopeTranslation.resolveTranslatedName(translations, lang: "ru", fallback: "Deep Cleaning"),
            "Deep Cleaning"
        )
    }

    func testFallsBackToStoredNameWhenTranslationNameIsBlank() {
        XCTAssertEqual(
            ScopeTranslation.resolveTranslatedName(translations, lang: "pl", fallback: "Deep Cleaning"),
            "Deep Cleaning"
        )
    }

    func testFallsBackToStoredNameWhenTranslationsAbsent() {
        XCTAssertEqual(
            ScopeTranslation.resolveTranslatedName(nil, lang: "cs", fallback: "Deep Cleaning"),
            "Deep Cleaning"
        )
        XCTAssertEqual(
            ScopeTranslation.resolveTranslatedName([:], lang: "cs", fallback: "Deep Cleaning"),
            "Deep Cleaning"
        )
    }

    func testFallsBackToStoredNameWhenLocaleBlank() {
        XCTAssertEqual(
            ScopeTranslation.resolveTranslatedName(translations, lang: nil, fallback: "Deep Cleaning"),
            "Deep Cleaning"
        )
        XCTAssertEqual(
            ScopeTranslation.resolveTranslatedName(translations, lang: "", fallback: "Deep Cleaning"),
            "Deep Cleaning"
        )
    }

    func testPreservesNilFallbackWhenNothingResolves() {
        XCTAssertNil(ScopeTranslation.resolveTranslatedName(nil, lang: "cs", fallback: nil))
    }

    func testDomainExtensionsResolveViaLocaleLanguageCode() {
        let service = OrderDetailService(id: "s1", name: "Deep Cleaning", translations: translations)
        XCTAssertEqual(service.localizedName(for: Locale(identifier: "cs-CZ")), "Hloubkové čištění")
        let package = OrderDetailPackage(id: "p1", name: "Deep Cleaning", price: nil, translations: translations)
        XCTAssertEqual(package.localizedName(for: Locale(identifier: "uk")), "Deep Cleaning")
    }

    func testTheLeadNamesTheFirstPackageOrServiceAndCountsTheRest() {
        var row = OrderListItem()
        row.selectedPackages = [PackageListItem(id: "p", name: " ")]
        row.selectedServices = [
            ServiceListItem(id: "a", name: "Bathroom Cleaning"), ServiceListItem(id: "b", name: "Upholstery Cleaning")
        ]
        let lead = OrdersBoardRules.leadName(row, locale: Locale(identifier: "en"))
        XCTAssertEqual(lead, "Bathroom Cleaning " + L10n.Offers.cardMore(1))
        XCTAssertEqual(
            OrdersBoardRules.lead(["Deep clean", "Bathroom", "Upholstery"]),
            "Deep clean " + L10n.Offers.cardMore(2)
        )
        XCTAssertEqual(OrdersBoardRules.lead(["Deep clean"]), "Deep clean")
        XCTAssertNil(OrdersBoardRules.lead([]))
    }
}
