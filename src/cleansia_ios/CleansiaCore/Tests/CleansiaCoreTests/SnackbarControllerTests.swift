import XCTest
@testable import CleansiaCore

@MainActor
final class SnackbarControllerTests: XCTestCase {
    func testShowErrorPublishesErrorSeverity() {
        let controller = SnackbarController()
        controller.showError("Something failed")

        let current = controller.current
        XCTAssertEqual(current?.text, "Something failed")
        XCTAssertEqual(current?.severity, .error)
    }

    func testConvenienceWrappersCarryTheirSeverity() {
        let controller = SnackbarController()

        controller.showSuccess("Saved")
        XCTAssertEqual(controller.current?.severity, .success)

        controller.showInfo("Heads up")
        XCTAssertEqual(controller.current?.severity, .info)

        controller.showWarning("Careful")
        XCTAssertEqual(controller.current?.severity, .warning)
    }

    func testNewMessageReplacesTheVisibleOne() {
        let controller = SnackbarController()
        controller.showInfo("first")
        let firstId = controller.current?.id

        controller.showError("second")
        XCTAssertEqual(controller.current?.text, "second")
        XCTAssertNotEqual(controller.current?.id, firstId)
    }

    func testDismissClearsTheCurrentMessage() {
        let controller = SnackbarController()
        controller.showError("boom")
        XCTAssertNotNil(controller.current)

        controller.dismiss()
        XCTAssertNil(controller.current)
    }

    func testDismissByIdOnlyClearsTheMatchingMessage() {
        let controller = SnackbarController()
        controller.showInfo("old")
        let oldId = controller.current?.id

        controller.showError("new")
        controller.dismiss(id: oldId ?? UUID())

        XCTAssertEqual(controller.current?.text, "new")
    }

    func testShowApiErrorRoutesThroughTheLocalizer() {
        let controller = SnackbarController(localizer: ApiErrorLocalizer())
        controller.showApiError(ApiError(code: nil, message: nil, httpStatus: 503))

        XCTAssertEqual(controller.current?.severity, .error)
        XCTAssertFalse(controller.current?.text.isEmpty ?? true)
    }

    func testShowApiErrorDropsCancellationSilently() {
        let controller = SnackbarController()
        controller.showApiError(ApiError(code: ApiError.cancelledCode))
        XCTAssertNil(controller.current)
    }

    func testShowApiErrorLeavesAnExistingMessageWhenACancellationArrives() {
        let controller = SnackbarController()
        controller.showError("real failure")
        controller.showApiError(ApiError(code: ApiError.cancelledCode))
        XCTAssertEqual(controller.current?.text, "real failure")
    }

    func testAutoDismissDurationIsLongerForErrors() {
        let error = SnackbarMessage(text: "e", severity: .error)
        let info = SnackbarMessage(text: "i", severity: .info)
        XCTAssertGreaterThan(error.autoDismissDuration, info.autoDismissDuration)
    }

    func testBottomInsetDefaultsFollowsSetAndReset() {
        let controller = SnackbarController()
        XCTAssertEqual(controller.bottomInset, SnackbarController.defaultBottomInset)

        controller.setBottomInset(100)
        XCTAssertEqual(controller.bottomInset, 100)

        controller.resetBottomInset()
        XCTAssertEqual(controller.bottomInset, SnackbarController.defaultBottomInset)
    }
}
