import Foundation
import XCTest
@testable import CleansiaCustomer

final class BookingSavedAddressChooserTests: XCTestCase {
    private func address(_ id: String, isDefault: Bool = false) -> SavedAddress {
        SavedAddressFixtures.address(id: id, isDefault: isDefault)
    }

    // MARK: - preselectedId

    func testPrefersTheCurrentBookingSavedAddressWhenItMatchesAKnownAddress() {
        let addresses = [address("a"), address("b", isDefault: true)]
        let id = BookingSavedAddressSelection.preselectedId(
            addresses: addresses,
            currentSavedAddressId: "a",
            repoSelectedId: "b"
        )
        XCTAssertEqual(id, "a")
    }

    func testFallsBackToRepoSelectedWhenCurrentIsUnknown() {
        let addresses = [address("a"), address("b", isDefault: true)]
        let id = BookingSavedAddressSelection.preselectedId(
            addresses: addresses,
            currentSavedAddressId: "gone",
            repoSelectedId: "a"
        )
        XCTAssertEqual(id, "a")
    }

    func testFallsBackToTheDefaultWhenNoSelection() {
        let addresses = [address("a"), address("b", isDefault: true)]
        let id = BookingSavedAddressSelection.preselectedId(
            addresses: addresses,
            currentSavedAddressId: nil,
            repoSelectedId: nil
        )
        XCTAssertEqual(id, "b")
    }

    func testFallsBackToTheFirstWhenNoSelectionAndNoDefault() {
        let addresses = [address("a"), address("b")]
        let id = BookingSavedAddressSelection.preselectedId(
            addresses: addresses,
            currentSavedAddressId: nil,
            repoSelectedId: nil
        )
        XCTAssertEqual(id, "a")
    }

    func testReturnsNilForAnEmptyList() {
        let id = BookingSavedAddressSelection.preselectedId(
            addresses: [],
            currentSavedAddressId: "a",
            repoSelectedId: "b"
        )
        XCTAssertNil(id)
    }

    // MARK: - applied (Android onAddressSelected parity)

    func testApplyingASavedAddressKeepsSavedAddressIdAndMarksHandPicked() {
        var current = BookingState()
        current.hydratedFromSavedId = "old"
        let next = BookingSavedAddressApply.applied(current, address: address("a"))

        XCTAssertEqual(next.street, "Main 1")
        XCTAssertEqual(next.city, "Prague")
        XCTAssertEqual(next.zipCode, "11000")
        XCTAssertEqual(next.savedAddressId, "a")
        XCTAssertNil(next.hydratedFromSavedId)
    }

    func testApplyingASavedAddressLeavesUnrelatedStateUntouched() {
        var current = BookingState()
        current.rooms = 3
        current.selectedTime = "10:00"
        let next = BookingSavedAddressApply.applied(current, address: address("a"))

        XCTAssertEqual(next.rooms, 3)
        XCTAssertEqual(next.selectedTime, "10:00")
    }
}
