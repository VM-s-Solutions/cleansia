import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The catalog is the sharpest case in the class: its rows are alternatives to each other and each
/// card renders its own price, so both a dropped row and a zeroed one misinform a customer who is
/// about to be charged the server's number.
///
/// Nullability is read from the C# records (`ServiceListItem`, `PackageListItem`, `ExtraListItem` —
/// every price a non-nullable `decimal`), never from the spec.
final class CatalogWireContractTests: XCTestCase {
    private func servicePayload() -> ServiceListItem {
        ServiceListItem(
            id: "svc-1",
            name: "Standard clean",
            description: "A clean",
            category: CategoryDto(id: "cat-1", slug: "home", name: "Home", displayOrder: 1),
            basePrice: 500,
            perRoomPrice: 100,
            translations: [:]
        )
    }

    private func packagePayload() -> PackageListItem {
        PackageListItem(
            id: "pkg-1",
            name: "Deep clean",
            description: "Deep",
            price: 900,
            translations: [:],
            includedServices: [PackageServiceSummary(name: "Windows", translations: [:])]
        )
    }

    private func extraPayload() -> ExtraListItem {
        ExtraListItem(
            id: "extra-1",
            slug: "oven",
            name: "Inside oven",
            description: nil,
            price: 250,
            displayOrder: 2,
            translations: [:]
        )
    }

    private func currencyPayload(code: String = "EUR", isDefault: Bool = true) -> CurrencyListItem {
        CurrencyListItem(id: "cur-\(code)", code: code, symbol: code, name: code, isDefault: isDefault)
    }

    func testFullyPopulatedCatalogRowsMap() throws {
        XCTAssertEqual(try CatalogService(servicePayload()).basePrice, 500)
        XCTAssertEqual(try CatalogPackage(packagePayload()).price, 900)
        XCTAssertEqual(try CatalogExtra(extraPayload()).price, 250)
        XCTAssertEqual(try CatalogCurrency(currencyPayload()).code, "EUR")
    }

    func testTheCatalogueIsLabelledWithTheOverviewsDefaultRow() throws {
        let rows = [currencyPayload(code: "CZK", isDefault: false), currencyPayload(code: "EUR", isDefault: true)]

        XCTAssertEqual(try CatalogCurrency.defaultRow(in: rows).code, "EUR")
    }

    /// A price list whose currency is unknown is a price list the customer was never shown: the old
    /// `"CZK"` literal labelled it by construction, which is the defect this closes.
    func testAnOverviewWithNoDefaultRefusesRatherThanLabellingTheCatalogueByGuess() {
        assertRefused("CurrencyListItem[isDefault]") {
            try CatalogCurrency.defaultRow(in: [currencyPayload(code: "CZK", isDefault: false)])
        }
        assertRefused("CurrencyListItem[isDefault]") { try CatalogCurrency.defaultRow(in: []) }
    }

    func testACurrencyRowWithNoCodeRefusesTheCatalogue() {
        for (field, break_) in [
            ("code", { (dto: inout CurrencyListItem) in dto.code = " " }),
            ("id", { dto in dto.id = nil }),
            ("isDefault", { dto in dto.isDefault = nil })
        ] {
            var payload = currencyPayload()
            break_(&payload)
            assertRefused(field) { try CatalogCurrency.defaultRow(in: [payload]) }
        }
    }

    func testAServiceWithNoPriceRefusesRatherThanQuotingFree() {
        for (field, break_) in [
            ("basePrice", { (dto: inout ServiceListItem) in dto.basePrice = nil }),
            ("perRoomPrice", { dto in dto.perRoomPrice = nil }),
            ("id", { dto in dto.id = "" }),
            ("name", { dto in dto.name = nil }),
            ("category", { dto in dto.category = nil })
        ] {
            var payload = servicePayload()
            break_(&payload)
            assertRefused(field) { try CatalogService(payload) }
        }
    }

    func testAPackageWithNoPriceRefusesTheWholeCatalogPage() {
        for (field, break_) in [
            ("price", { (dto: inout PackageListItem) in dto.price = nil }),
            ("id", { dto in dto.id = nil }),
            ("name", { dto in dto.name = "  " })
        ] {
            var payload = packagePayload()
            break_(&payload)
            assertRefused(field) { try [payload].map(CatalogPackage.init) }
        }
    }

    func testAnExtraWithNoPriceRefusesRatherThanLookingFree() {
        for (field, break_) in [
            ("price", { (dto: inout ExtraListItem) in dto.price = nil }),
            ("displayOrder", { dto in dto.displayOrder = nil }),
            ("slug", { dto in dto.slug = nil })
        ] {
            var payload = extraPayload()
            break_(&payload)
            assertRefused(field) { try CatalogExtra(payload) }
        }
    }

    /// A row is never quietly dropped here: the whole page refuses, so the customer is told rather
    /// than shown a menu missing an item they could have bought.
    func testOneBrokenRowRefusesThePageRatherThanShorteningIt() {
        var broken = servicePayload()
        broken.basePrice = nil
        assertRefused("basePrice") { try [servicePayload(), broken].map(CatalogService.init) }
    }

    private func assertRefused(
        _ field: String,
        file: StaticString = #filePath,
        line: UInt = #line,
        _ map: () throws -> some Any
    ) {
        XCTAssertThrowsError(try map(), "\(field) was supplied a value instead of refusing", file: file, line: line) {
            XCTAssertEqual(
                $0 as? WireContractViolation,
                WireContractViolation(field: field),
                "the refusal must name \(field)",
                file: file,
                line: line
            )
        }
    }
}
