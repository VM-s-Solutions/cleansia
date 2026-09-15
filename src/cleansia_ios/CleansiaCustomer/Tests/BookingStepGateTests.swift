import XCTest
@testable import CleansiaCustomer

final class BookingStepGateTests: XCTestCase {
    func testStepOneNeedsAServiceOrPackageAndAtLeastOneRoom() {
        var state = BookingState()
        XCTAssertFalse(BookingStepGate.canContinue(step: 1, state: state, alreadyConsented: false))

        state.selectedServiceIds = ["s-1"]
        XCTAssertTrue(BookingStepGate.canContinue(step: 1, state: state, alreadyConsented: false))
    }

    func testStepOnePackageOnlyAlsoPasses() {
        var state = BookingState()
        state.selectedPackageIds = ["p-1"]
        XCTAssertTrue(BookingStepGate.canContinue(step: 1, state: state, alreadyConsented: false))
    }

    func testStepOneFailsWhenRoomsBelowOne() {
        var state = BookingState()
        state.selectedServiceIds = ["s-1"]
        state.rooms = 0
        XCTAssertFalse(BookingStepGate.canContinue(step: 1, state: state, alreadyConsented: false))
    }

    func testStepTwoNeedsStreetDateAndTime() {
        var state = BookingState()
        state.street = "Wenceslas"
        XCTAssertFalse(BookingStepGate.canContinue(step: 2, state: state, alreadyConsented: false))

        state.selectedDate = "2026-07-01"
        XCTAssertFalse(BookingStepGate.canContinue(step: 2, state: state, alreadyConsented: false))

        state.selectedTime = "10:00"
        XCTAssertTrue(BookingStepGate.canContinue(step: 2, state: state, alreadyConsented: false))
    }

    func testStepTwoFailsWhenStreetIsBlankWhitespace() {
        var state = BookingState()
        state.street = "   "
        state.selectedDate = "2026-07-01"
        state.selectedTime = "10:00"
        XCTAssertFalse(BookingStepGate.canContinue(step: 2, state: state, alreadyConsented: false))
    }

    func testStepThreeNeedsAPaymentMethod() {
        var state = BookingState()
        XCTAssertFalse(BookingStepGate.canContinue(step: 3, state: state, alreadyConsented: true))

        state.paymentMethod = .cash
        XCTAssertTrue(BookingStepGate.canContinue(step: 3, state: state, alreadyConsented: true))
    }

    /// The review step's terms box gates the slide-to-confirm the way the web wizard's place-order
    /// button is gated: a payment method is not enough while the box is shown and unticked.
    func testStepThreeNeedsTheTermsTickWhenTheAccountHasNotConsentedYet() {
        var state = BookingState()
        state.paymentMethod = .cash
        XCTAssertFalse(BookingStepGate.canContinue(step: 3, state: state, alreadyConsented: false))

        state.termsAccepted = true
        XCTAssertTrue(BookingStepGate.canContinue(step: 3, state: state, alreadyConsented: false))
    }

    func testStepThreeSkipsTheTickForAnAccountThatAlreadyHoldsBothConsents() {
        var state = BookingState()
        state.paymentMethod = .cash
        state.termsAccepted = false
        XCTAssertTrue(BookingStepGate.canContinue(step: 3, state: state, alreadyConsented: true))
    }

    func testATickAloneNeverStandsInForAPaymentMethod() {
        var state = BookingState()
        state.termsAccepted = true
        XCTAssertFalse(BookingStepGate.canContinue(step: 3, state: state, alreadyConsented: false))
    }

    func testUnknownStepNeverContinues() {
        XCTAssertFalse(BookingStepGate.canContinue(step: 0, state: BookingState(), alreadyConsented: false))
        XCTAssertFalse(BookingStepGate.canContinue(step: 4, state: BookingState(), alreadyConsented: false))
    }

    func testTotalStepsIsThree() {
        XCTAssertEqual(BookingStepGate.totalSteps, 3)
    }
}
