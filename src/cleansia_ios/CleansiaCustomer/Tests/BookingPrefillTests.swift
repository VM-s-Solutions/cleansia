import CleansiaCustomerApi
import Foundation
import XCTest
@testable import CleansiaCustomer

final class BookingPrefillTests: XCTestCase {
    // MARK: - hydratedWithPreferred (BookingBottomSheet.kt:270-282)

    func testHydrationFillsABlankAddressFromThePreferred() {
        let preferred = SavedAddressFixtures.address(id: "a")
        let state = BookingPrefill.hydratedWithPreferred(BookingState(), preferred: preferred)

        XCTAssertEqual(state.street, "Main 1")
        XCTAssertEqual(state.city, "Prague")
        XCTAssertEqual(state.zipCode, "11000")
        XCTAssertEqual(state.savedAddressId, "a")
    }

    func testHydrationNeverOverwritesATypedStreet() {
        var current = BookingState()
        current.street = "Typed 5"
        let state = BookingPrefill.hydratedWithPreferred(current, preferred: SavedAddressFixtures.address(id: "a"))

        XCTAssertEqual(state.street, "Typed 5")
        XCTAssertNil(state.savedAddressId)
    }

    func testHydrationWithoutAPreferredIsANoOp() {
        let state = BookingPrefill.hydratedWithPreferred(BookingState(), preferred: nil)
        XCTAssertEqual(state, BookingState())
    }

    // MARK: - withPackage (BookingBottomSheet.kt:390-399 — union, keeps the rest)

    func testPackagePrefillAddsToTheExistingSelection() {
        var current = BookingState()
        current.selectedPackageIds = ["p0"]
        let state = BookingPrefill.withPackage(current, packageId: "p1")
        XCTAssertEqual(state.selectedPackageIds, ["p0", "p1"])
    }

    // MARK: - rebook (BookingBottomSheet.kt:305-374)

    func testRebookReplacesSelectionsAndCopiesTheOrderAddress() {
        var order = OrderFixtures.detail(id: "o1", statusValue: 5)
        order.selectedServices = [ServiceDetails(id: "s-1", name: "Deep clean")]
        order.selectedPackages = [PackageDetails(id: "p-1", name: "Move-out")]
        order.rooms = 4
        order.bathrooms = 2
        order.address = OrderAddress(street: "Old 9", city: "Brno", zipCode: "60200")

        var current = BookingState()
        current.selectedServiceIds = ["stale"]
        current.countryIsoCode = "cz"

        let result = BookingPrefill.rebook(
            current,
            order: order,
            savedAddresses: [],
            catalog: CatalogFixtures.populated
        )

        XCTAssertEqual(result.state.selectedServiceIds, ["s-1"])
        XCTAssertEqual(result.state.selectedPackageIds, ["p-1"])
        XCTAssertEqual(result.state.rooms, 4)
        XCTAssertEqual(result.state.bathrooms, 2)
        XCTAssertEqual(result.state.street, "Old 9")
        XCTAssertEqual(result.state.city, "Brno")
        XCTAssertEqual(result.state.zipCode, "60200")
        XCTAssertEqual(result.state.countryIsoCode, "cz")
        XCTAssertNil(result.state.savedAddressId)
        XCTAssertFalse(result.droppedUnavailableItems)
    }

    func testRebookDropsRetiredCatalogItemsAndFlagsIt() {
        var order = OrderFixtures.detail(id: "o1", statusValue: 5)
        order.selectedServices = [
            ServiceDetails(id: "s-1", name: "Kept"),
            ServiceDetails(id: "s-gone", name: "Retired")
        ]
        order.selectedPackages = [PackageDetails(id: "p-gone", name: "Retired")]

        let result = BookingPrefill.rebook(
            BookingState(),
            order: order,
            savedAddresses: [],
            catalog: CatalogFixtures.populated
        )

        XCTAssertEqual(result.state.selectedServiceIds, ["s-1"])
        XCTAssertEqual(result.state.selectedPackageIds, [])
        XCTAssertTrue(result.droppedUnavailableItems)
    }

    func testRebookTrustsOriginalIdsWhenTheCatalogIsNotReady() {
        var order = OrderFixtures.detail(id: "o1", statusValue: 5)
        order.selectedServices = [ServiceDetails(id: "s-unknown", name: "Any")]

        for catalog in [nil, Catalog.empty] {
            let result = BookingPrefill.rebook(BookingState(), order: order, savedAddresses: [], catalog: catalog)
            XCTAssertEqual(result.state.selectedServiceIds, ["s-unknown"])
            XCTAssertFalse(result.droppedUnavailableItems)
        }
    }

    func testRebookKeepsCurrentRoomsWhenTheOrderCarriesNone() {
        var order = OrderFixtures.detail(id: "o1", statusValue: 5)
        order.rooms = 0
        order.bathrooms = nil

        var current = BookingState()
        current.rooms = 3
        current.bathrooms = 2

        let result = BookingPrefill.rebook(current, order: order, savedAddresses: [], catalog: nil)
        XCTAssertEqual(result.state.rooms, 3)
        XCTAssertEqual(result.state.bathrooms, 2)
    }

    func testRebookMatchesASavedAddressCaseInsensitively() {
        var order = OrderFixtures.detail(id: "o1", statusValue: 5)
        order.address = OrderAddress(street: "MAIN 1", city: "prague", zipCode: "11000")

        let saved = SavedAddressFixtures.address(id: "saved-1")
        let result = BookingPrefill.rebook(BookingState(), order: order, savedAddresses: [saved], catalog: nil)

        XCTAssertEqual(result.state.savedAddressId, "saved-1")
    }
}
