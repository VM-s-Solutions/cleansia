import XCTest
@testable import CleansiaCustomer

@MainActor
final class BookingViewModelTests: XCTestCase {
    func testStartsOnStepOne() {
        let vm = BookingViewModel()
        XCTAssertEqual(vm.currentStep, 1)
        XCTAssertTrue(vm.isFirstStep)
        XCTAssertFalse(vm.isLastStep)
    }

    func testAdvanceWalksOneTwoThreeAndStopsAtThree() {
        let vm = BookingViewModel()

        XCTAssertTrue(vm.advance())
        XCTAssertEqual(vm.currentStep, 2)

        XCTAssertTrue(vm.advance())
        XCTAssertEqual(vm.currentStep, 3)
        XCTAssertTrue(vm.isLastStep)

        XCTAssertFalse(vm.advance())
        XCTAssertEqual(vm.currentStep, 3)
    }

    func testBackWalksThreeTwoOneAndStopsAtOne() {
        let vm = BookingViewModel()
        vm.advance()
        vm.advance()
        XCTAssertEqual(vm.currentStep, 3)

        XCTAssertTrue(vm.back())
        XCTAssertEqual(vm.currentStep, 2)

        XCTAssertTrue(vm.back())
        XCTAssertEqual(vm.currentStep, 1)

        XCTAssertFalse(vm.back())
        XCTAssertEqual(vm.currentStep, 1)
    }

    func testBackOnStepOneDoesNotMoveSoTheViewCanClose() {
        let vm = BookingViewModel()
        XCTAssertTrue(vm.isFirstStep)
        XCTAssertFalse(vm.back())
        XCTAssertEqual(vm.currentStep, 1)
    }

    func testUpdateRebuildsStateViaCopy() {
        let vm = BookingViewModel()
        vm.update { current in
            var next = current
            next.rooms = 3
            return next
        }
        XCTAssertEqual(vm.state.rooms, 3)
    }

    func testResetReturnsToStepOneAndCleanState() {
        let vm = BookingViewModel()
        vm.update { current in
            var next = current
            next.selectedServiceIds = ["s-1"]
            next.street = "X"
            return next
        }
        vm.advance()
        vm.advance()

        vm.reset()

        XCTAssertEqual(vm.currentStep, 1)
        XCTAssertEqual(vm.state, BookingState())
        XCTAssertEqual(vm.submitState, .idle)
        XCTAssertEqual(vm.quoteState, .idle)
        XCTAssertEqual(vm.promoState, .idle)
        XCTAssertEqual(vm.referralState, .idle)
    }

    func testInitialSealedStatesAreIdle() {
        let vm = BookingViewModel()
        XCTAssertEqual(vm.submitState, .idle)
        XCTAssertEqual(vm.quoteState, .idle)
        XCTAssertEqual(vm.promoState, .idle)
        XCTAssertEqual(vm.referralState, .idle)
    }
}
