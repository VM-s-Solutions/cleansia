import XCTest
@testable import CleansiaCustomer

/// The form's two shipped holes, both invisible to a view-model suite: setters
/// that compile, are covered, and are wired to nothing. `setRooms` had exactly
/// one caller in the tree and it was a test; `setBathrooms` had none, so a
/// four-bedroom flat silently booked a 2/1 clean on every blank create.
final class CreateRecurringBindingTests: XCTestCase {
    private static let screen = "CleansiaCustomer/Sources/Features/Recurring/CreateRecurringScreen.swift"

    func testTheFormWiresBothPropertySteppers() throws {
        let source = try read(Self.screen)
        XCTAssertTrue(source.contains("onRoomsChange: vm.setRooms"), "rooms is not editable")
        XCTAssertTrue(source.contains("onBathroomsChange: vm.setBathrooms"), "bathrooms is not editable")
    }

    /// A customer with no saved address could reach this form (Home, the Plus
    /// success screen, the empty recurring list) and find nothing actionable.
    func testTheAddressSectionOffersAWayOut() throws {
        let source = try read(Self.screen)
        XCTAssertTrue(
            source.contains("AddAddressRow(onTap: onAddAddress)"),
            "the address section has no add affordance"
        )
        XCTAssertTrue(source.contains("AddressManagerView("), "the add affordance opens no address surface")
        XCTAssertTrue(source.contains("vm.setSavedAddressId(address.id)"), "a picked address is never applied")
    }

    func testTheScreenSpellsNoLabelItself() throws {
        let source = try read(Self.screen)
        for hardcoded in ["Rooms", "Bathrooms", "Add new"] {
            XCTAssertFalse(
                source.contains("\"\(hardcoded)"),
                "\(hardcoded) is a literal — cs/sk/uk/ru would read it in English"
            )
        }
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
