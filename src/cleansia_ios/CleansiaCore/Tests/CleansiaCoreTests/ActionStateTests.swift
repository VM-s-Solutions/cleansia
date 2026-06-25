import XCTest
@testable import CleansiaCore

final class ActionStateTests: XCTestCase {
    func testIdleIsNotSubmittingAndHasNoError() {
        let state = ActionState.idle
        XCTAssertFalse(state.isSubmitting)
        XCTAssertNil(state.errorMessage)
    }

    func testSubmittingReportsSubmitting() {
        let state = ActionState.submitting
        XCTAssertTrue(state.isSubmitting)
        XCTAssertNil(state.errorMessage)
    }

    func testErrorCarriesLocalizedMessage() {
        let state = ActionState.error("Could not cancel order")
        XCTAssertFalse(state.isSubmitting)
        XCTAssertEqual(state.errorMessage, "Could not cancel order")
    }

    func testSubmitLifecycleTransitions() {
        var state = ActionState.idle
        state = .submitting
        XCTAssertTrue(state.isSubmitting)
        state = .idle
        XCTAssertEqual(state, .idle)

        state = .submitting
        state = .error("boom")
        XCTAssertEqual(state, .error("boom"))
    }
}
