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

    /// The customer states the level themselves: nothing is preselected, so the step holds until one
    /// is picked, and Normal is as much an answer as Heavy.
    func testStepTwoNeedsAChosenLevel() {
        var state = BookingState()
        state.selectedServiceIds = ["s-1"]
        XCTAssertFalse(BookingStepGate.canContinue(step: 2, state: state, alreadyConsented: false))

        state.dirtiness = .normal
        XCTAssertTrue(BookingStepGate.canContinue(step: 2, state: state, alreadyConsented: false))

        state.dirtiness = .heavy
        XCTAssertTrue(BookingStepGate.canContinue(step: 2, state: state, alreadyConsented: false))
    }

    func testStepThreeNeedsStreetDateAndTime() {
        var state = BookingState()
        state.street = "Wenceslas"
        XCTAssertFalse(BookingStepGate.canContinue(step: 3, state: state, alreadyConsented: false))

        state.selectedDate = "2026-07-01"
        XCTAssertFalse(BookingStepGate.canContinue(step: 3, state: state, alreadyConsented: false))

        state.selectedTime = "10:00"
        XCTAssertTrue(BookingStepGate.canContinue(step: 3, state: state, alreadyConsented: false))
    }

    func testStepThreeFailsWhenStreetIsBlankWhitespace() {
        var state = BookingState()
        state.street = "   "
        state.selectedDate = "2026-07-01"
        state.selectedTime = "10:00"
        XCTAssertFalse(BookingStepGate.canContinue(step: 3, state: state, alreadyConsented: false))
    }

    func testStepFourNeedsAPaymentMethod() {
        var state = BookingState()
        state.earlyPerformanceRequested = true
        XCTAssertFalse(BookingStepGate.canContinue(step: 4, state: state, alreadyConsented: true))

        state.paymentMethod = .cash
        XCTAssertTrue(BookingStepGate.canContinue(step: 4, state: state, alreadyConsented: true))
    }

    /// The review step's terms box gates the slide-to-confirm the way the web wizard's place-order
    /// button is gated: a payment method is not enough while the box is shown and unticked.
    func testStepFourNeedsTheTermsTickWhenTheAccountHasNotConsentedYet() {
        var state = BookingState()
        state.paymentMethod = .cash
        state.earlyPerformanceRequested = true
        XCTAssertFalse(BookingStepGate.canContinue(step: 4, state: state, alreadyConsented: false))

        state.termsAccepted = true
        XCTAssertTrue(BookingStepGate.canContinue(step: 4, state: state, alreadyConsented: false))
    }

    func testStepFourSkipsTheTickForAnAccountThatAlreadyHoldsBothConsents() {
        var state = BookingState()
        state.paymentMethod = .cash
        state.termsAccepted = false
        state.earlyPerformanceRequested = true
        XCTAssertTrue(BookingStepGate.canContinue(step: 4, state: state, alreadyConsented: true))
    }

    /// Asked on every booking: an account that already holds both consents is asked for it all the same.
    func testStepFourNeedsTheEarlyPerformanceRequestOnEveryBooking() {
        var state = BookingState()
        state.paymentMethod = .cash
        state.termsAccepted = true
        XCTAssertFalse(BookingStepGate.canContinue(step: 4, state: state, alreadyConsented: true))
        XCTAssertFalse(BookingStepGate.canContinue(step: 4, state: state, alreadyConsented: false))

        state.earlyPerformanceRequested = true
        XCTAssertTrue(BookingStepGate.canContinue(step: 4, state: state, alreadyConsented: true))
        XCTAssertTrue(BookingStepGate.canContinue(step: 4, state: state, alreadyConsented: false))
    }

    func testATickAloneNeverStandsInForAPaymentMethod() {
        var state = BookingState()
        state.termsAccepted = true
        XCTAssertFalse(BookingStepGate.canContinue(step: 4, state: state, alreadyConsented: false))
    }

    func testUnknownStepNeverContinues() {
        XCTAssertFalse(BookingStepGate.canContinue(step: 0, state: BookingState(), alreadyConsented: false))
        XCTAssertFalse(BookingStepGate.canContinue(step: 5, state: BookingState(), alreadyConsented: false))
    }

    func testTotalStepsIsFour() {
        XCTAssertEqual(BookingStepGate.totalSteps, 4)
    }

    /// The wizard steps itself back to When when a time stops holding — at the slide, after a card guarantee, on
    /// a return to the foreground — and that step back slides back like the back button does: the sheet reads
    /// the direction from the step change, not from which button was tapped (docs/mobile-app/patterns.md
    /// #booking-steps).
    func testEveryStepBackSlidesBackWhateverMovedIt() throws {
        let sheet = try sheetSource()
        XCTAssertTrue(
            sheet.contains(
                ".onChange(of: viewModel.currentStep) { next in DispatchQueue.main.async { " +
                    "movingForward = next > shownStep shownStep = next } }"
            ),
            "the direction is not read from the step change, or the page drawn moves with the wizard"
        )
        XCTAssertTrue(
            sheet.contains(
                "viewModel.currentStep == shownStep ? movingForward : viewModel.currentStep > shownStep"
            ),
            "the page on screen is not drawn with the way it is about to leave"
        )
        XCTAssertEqual(
            sheet.replacingOccurrences(of: "@State private var movingForward = true", with: "")
                .components(separatedBy: "movingForward =").count - 1,
            1,
            "something other than the step change decides the direction"
        )
        XCTAssertTrue(sheet.contains("private var step: Int { shownStep }"), "the page drawn moves with the wizard")
        XCTAssertTrue(
            sheet.contains("insertion: .move(edge: forward ? .trailing : .leading)") &&
                sheet.contains("removal: .move(edge: forward ? .leading : .trailing)"),
            "the slide does not follow the direction of the step change"
        )
    }

    /// The page leaving slides out while the next slides in. Its identity changes inside a container that
    /// outlives the change; with `.id` at the top of the step area the leaving page vanished in one frame
    /// (recorded on iOS 26.3 and 16.4) and only the incoming page moved.
    func testTheLeavingPageSlidesOutInsteadOfVanishing() throws {
        let sheet = try sheetSource()
        XCTAssertTrue(
            sheet.contains(
                "ZStack { stepPage .transition(stepTransition) .id(step) } " +
                    ".animation(.easeInOut(duration: 0.28), value: step)"
            ),
            "the page's identity does not change inside a container of its own, so the leaving page is dropped"
        )
        XCTAssertEqual(sheet.components(separatedBy: ".id(step)").count - 1, 1, "the page carries a second identity")
    }

    private func sheetSource() throws -> String {
        try String(
            contentsOf: URL(fileURLWithPath: #filePath)
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .appendingPathComponent("Sources/Features/Booking/BookingSheetView.swift"),
            encoding: .utf8
        ).replacingOccurrences(of: #"\s+"#, with: " ", options: .regularExpression)
    }
}
