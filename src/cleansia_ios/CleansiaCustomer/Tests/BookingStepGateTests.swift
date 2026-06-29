import XCTest
@testable import CleansiaCustomer

final class BookingStepGateTests: XCTestCase {
    func testStepOneNeedsAServiceOrPackageAndAtLeastOneRoom() {
        var state = BookingState()
        XCTAssertFalse(BookingStepGate.canContinue(step: 1, state: state))

        state.selectedServiceIds = ["s-1"]
        XCTAssertTrue(BookingStepGate.canContinue(step: 1, state: state))
    }

    func testStepOnePackageOnlyAlsoPasses() {
        var state = BookingState()
        state.selectedPackageIds = ["p-1"]
        XCTAssertTrue(BookingStepGate.canContinue(step: 1, state: state))
    }

    func testStepOneFailsWhenRoomsBelowOne() {
        var state = BookingState()
        state.selectedServiceIds = ["s-1"]
        state.rooms = 0
        XCTAssertFalse(BookingStepGate.canContinue(step: 1, state: state))
    }

    func testStepTwoNeedsStreetDateAndTime() {
        var state = BookingState()
        state.street = "Wenceslas"
        XCTAssertFalse(BookingStepGate.canContinue(step: 2, state: state))

        state.selectedDate = "2026-07-01"
        XCTAssertFalse(BookingStepGate.canContinue(step: 2, state: state))

        state.selectedTime = "10:00"
        XCTAssertTrue(BookingStepGate.canContinue(step: 2, state: state))
    }

    func testStepTwoFailsWhenStreetIsBlankWhitespace() {
        var state = BookingState()
        state.street = "   "
        state.selectedDate = "2026-07-01"
        state.selectedTime = "10:00"
        XCTAssertFalse(BookingStepGate.canContinue(step: 2, state: state))
    }

    func testStepThreeNeedsAPaymentMethod() {
        var state = BookingState()
        XCTAssertFalse(BookingStepGate.canContinue(step: 3, state: state))

        state.paymentMethod = .cash
        XCTAssertTrue(BookingStepGate.canContinue(step: 3, state: state))
    }

    func testUnknownStepNeverContinues() {
        XCTAssertFalse(BookingStepGate.canContinue(step: 0, state: BookingState()))
        XCTAssertFalse(BookingStepGate.canContinue(step: 4, state: BookingState()))
    }

    func testTotalStepsIsThree() {
        XCTAssertEqual(BookingStepGate.totalSteps, 3)
    }
}
