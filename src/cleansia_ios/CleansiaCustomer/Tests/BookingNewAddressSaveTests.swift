import CleansiaCore
import XCTest
@testable import CleansiaCustomer

final class BookingNewAddressSaveTests: XCTestCase {
    private func geocoded(street: String = "Main 1", city: String = "Prague") -> GeocodedAddress {
        SavedAddressFixtures.geocoded(street: street, city: city)
    }

    func testNoDraftWhenSaveIsOff() {
        let draft = BookingNewAddressSave.draft(
            from: geocoded(),
            label: "Office",
            save: false,
            setAsDefault: true,
            fallbackLabel: "Saved"
        )
        XCTAssertNil(draft)
    }

    func testBuildsDraftFromAddressWhenSaving() {
        let draft = BookingNewAddressSave.draft(
            from: geocoded(),
            label: "  Office  ",
            save: true,
            setAsDefault: false,
            fallbackLabel: "Saved"
        )
        XCTAssertEqual(draft?.label, "Office")
        XCTAssertEqual(draft?.street, "Main 1")
        XCTAssertEqual(draft?.city, "Prague")
        XCTAssertEqual(draft?.zipCode, "11000")
        XCTAssertEqual(draft?.setAsDefault, false)
    }

    func testFallsBackToFallbackLabelWhenBlank() {
        let draft = BookingNewAddressSave.draft(
            from: geocoded(),
            label: "   ",
            save: true,
            setAsDefault: false,
            fallbackLabel: "Saved"
        )
        XCTAssertEqual(draft?.label, "Saved")
    }

    func testPropagatesSetAsDefault() {
        let draft = BookingNewAddressSave.draft(
            from: geocoded(),
            label: "Home",
            save: true,
            setAsDefault: true,
            fallbackLabel: "Saved"
        )
        XCTAssertEqual(draft?.setAsDefault, true)
    }

    func testDerivesStreetFromFormattedWhenStreetBlank() {
        let address = GeocodedAddress(
            latitude: 50,
            longitude: 14,
            street: "",
            city: "Prague",
            zipCode: "11000",
            country: "Czechia",
            countryIsoCode: "cz",
            formatted: "Main 1, Prague"
        )
        let draft = BookingNewAddressSave.draft(
            from: address,
            label: "Home",
            save: true,
            setAsDefault: false,
            fallbackLabel: "Saved"
        )
        XCTAssertEqual(draft?.street, "Main 1")
    }
}
