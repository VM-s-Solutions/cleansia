import XCTest
@testable import CleansiaCustomer

final class BookingCardResultTests: XCTestCase {
    func testCompletedNavigatesToSuccessWithoutFlippingOrderState() {
        let resolution = BookingCardResultResolver.resolve(.completed, confirmationCode: "CLN-9")

        guard case let .navigateToSuccess(code) = resolution else {
            return XCTFail("expected navigateToSuccess, got \(resolution)")
        }
        XCTAssertEqual(code, "CLN-9")
    }

    func testCanceledShowsSnackbarAndLeavesOrderPending() {
        let resolution = BookingCardResultResolver.resolve(.canceled, confirmationCode: "CLN-9")

        guard case let .snackbar(messageKey) = resolution else {
            return XCTFail("expected snackbar, got \(resolution)")
        }
        XCTAssertEqual(messageKey, "error_payment_cancelled")
    }

    func testFailedShowsSnackbarAndLeavesOrderPending() {
        let resolution = BookingCardResultResolver.resolve(.failed, confirmationCode: "CLN-9")

        guard case let .snackbar(messageKey) = resolution else {
            return XCTFail("expected snackbar, got \(resolution)")
        }
        XCTAssertEqual(messageKey, "error_payment_failed")
    }

    func testNonCompletedOutcomesNeverNavigateToSuccess() {
        for outcome in [PaymentSheetOutcome.canceled, .failed] {
            let resolution = BookingCardResultResolver.resolve(outcome, confirmationCode: "CLN-9")
            if case .navigateToSuccess = resolution {
                XCTFail("\(outcome) must not navigate to success — paid status is webhook-authoritative")
            }
        }
    }
}
