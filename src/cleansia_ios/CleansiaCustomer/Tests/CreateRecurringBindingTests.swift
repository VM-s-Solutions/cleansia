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

    /// The view model resolving the notice proves nothing about the form drawing it, and an edit that
    /// silently leaves already-booked cleanings behind is exactly the claim a customer must not have to
    /// discover.
    func testTheFormDrawsTheEditAppliesNotice() throws {
        let source = try read(Self.screen)
        XCTAssertTrue(
            source.contains("if let appliesNotice = vm.appliesNotice"),
            "the form never asks whether it owes the customer the edit notice"
        )
        XCTAssertTrue(source.contains("AppliesNotice(text: appliesNotice)"), "the notice is resolved and dropped")
    }

    /// The view model keeping the favourite cleaner proves nothing if the form never says why the save
    /// failed or offers the one way through.
    func testTheFormOffersSavingWithoutARefusedFavouriteCleaner() throws {
        let source = try read(Self.screen)
        XCTAssertTrue(source.contains("if vm.preferredCleanerRefused {"), "the refusal is never drawn")
        XCTAssertTrue(source.contains("text: L10n.Recurring.preferredCleanerRefused"), "the refusal says nothing")
        XCTAssertTrue(
            source.contains("await vm.saveWithoutPreferredCleaner()"),
            "the save-without action calls nothing"
        )
    }

    func testTheStartPickerIsBoundedByTheViewModelsRange() throws {
        let source = try read(Self.screen)
        XCTAssertTrue(source.contains("range: vm.startRange"), "the start picker is not given the end-date cap")
        XCTAssertTrue(source.contains("in: range,"), "the start picker ignores the range it is given")
    }

    /// A free time wheel offered 03:07, which the server refuses and the materialiser would book. The
    /// schedule's time is now the booking's part-of-day picker, still over the bookable starts only.
    func testTheTimePickerOffersOnlyBookableStarts() throws {
        let source = try read(Self.screen)
        XCTAssertTrue(
            source.contains("RecurringTime.bookableTimes.map { BookingTimeSlot(time: $0, state: .available) }"),
            "the picker is not built from the bookable starts"
        )
        XCTAssertTrue(
            source.contains("DayPartTimePicker(slots: Self.slots, selectedTime: time, onSelect: onChange)"),
            "the bookable starts are not on the part-of-day picker"
        )
        XCTAssertFalse(source.contains("displayedComponents: .hourAndMinute"), "a free time wheel is back")
    }

    func testTheFormOffersTheFavouriteCleanerPickerBoundToTheForm() throws {
        let source = try read(Self.screen)
        XCTAssertTrue(source.contains("cleaners: vm.servingCleaners"), "the picker is not given the served list")
        XCTAssertTrue(
            source.contains("selectedId: vm.formState.preferredEmployeeId"),
            "the picker does not show the form's favourite cleaner"
        )
        XCTAssertTrue(source.contains("onSelect: vm.setPreferredEmployeeId"), "a pick is never applied")
    }

    /// The server always accepted a favourite cleaner on create; no client ever sent one.
    func testTheCreateCommandCarriesTheFavouriteCleaner() throws {
        let source = try read("CleansiaCustomer/Sources/Features/Recurring/Data/RecurringBookingClient.swift")
        let start = try XCTUnwrap(source.range(of: "func create(", options: .backwards), "no create call")
        let end = try XCTUnwrap(source.range(of: "func update(", options: .backwards), "no update call")
        let create = source[start.upperBound ..< end.lowerBound]
        XCTAssertTrue(
            create.contains("preferredEmployeeId: input.preferredEmployeeId"),
            "the create command drops the favourite cleaner"
        )
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
