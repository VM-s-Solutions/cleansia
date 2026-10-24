import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class SecurityViewModelTests: XCTestCase {
    private var client: FakeChangePasswordClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakeChangePasswordClient()
        snackbar = SnackbarController()
    }

    private func makeVM(email: String = "jane@example.com") -> SecurityViewModel {
        SecurityViewModel(email: email, language: "en", client: client, snackbar: snackbar)
    }

    func testRequestCodeCallsClientAndAdvancesStep() async {
        client.requestCodeResult = .success(())
        let vm = makeVM()

        await vm.requestCode()

        XCTAssertEqual(client.requestedEmails, ["jane@example.com"])
        XCTAssertTrue(vm.codeRequested)
        XCTAssertNotNil(snackbar.current)
    }

    func testRequestCodeFailureStaysOnStepOne() async {
        client.requestCodeResult = .failure(ApiError(httpStatus: 400))
        let vm = makeVM()

        await vm.requestCode()

        XCTAssertFalse(vm.codeRequested)
        guard case .error = vm.requestState else { return XCTFail("expected action error") }
    }

    func testChangePasswordRejectsWeakPasswordWithoutCallingClient() async {
        let vm = makeVM()
        vm.codeRequested = true

        await vm.changePassword(code: "123456", newPassword: "short", confirmPassword: "short")

        XCTAssertTrue(client.changeCalls.isEmpty)
        guard case .error = vm.changeState else { return XCTFail("expected validation error") }
    }

    func testChangePasswordRejectsMismatchWithoutCallingClient() async {
        let vm = makeVM()
        vm.codeRequested = true

        await vm.changePassword(code: "123456", newPassword: "newpass123", confirmPassword: "different123")

        XCTAssertTrue(client.changeCalls.isEmpty)
        guard case .error = vm.changeState else { return XCTFail("expected validation error") }
    }

    func testChangePasswordSuccessCallsClientAndEmitsDone() async {
        client.changePasswordResult = .success(())
        let vm = makeVM()
        vm.codeRequested = true

        var done = false
        let token = vm.passwordChanged.sink { done = true }
        defer { token.cancel() }

        await vm.changePassword(code: "999888", newPassword: "newpass123", confirmPassword: "newpass123")

        XCTAssertEqual(client.changeCalls.count, 1)
        XCTAssertEqual(client.changeCalls[0].email, "jane@example.com")
        XCTAssertEqual(client.changeCalls[0].code, "999888")
        XCTAssertEqual(client.changeCalls[0].newPassword, "newpass123")
        XCTAssertTrue(done)
    }

    /// A code pasted from the e-mail brings its spacing and the words around it, and the server charges one of
    /// the code's attempts for anything it is sent, so only the digits go.
    func testChangePasswordSendsOnlyTheDigitsOfAPastedCode() async {
        for pasted in ["123 456", "Code: 123456", " 123456 "] {
            let vm = makeVM()
            vm.codeRequested = true
            await vm.changePassword(code: pasted, newPassword: "newpass123", confirmPassword: "newpass123")
        }

        XCTAssertEqual(client.changeCalls.map(\.code), ["123456", "123456", "123456"])
    }

    func testChangePasswordServerFailureSurfacesError() async {
        client.changePasswordResult = .failure(ApiError(httpStatus: 400))
        let vm = makeVM()
        vm.codeRequested = true

        await vm.changePassword(code: "999888", newPassword: "newpass123", confirmPassword: "newpass123")

        XCTAssertNotNil(snackbar.current)
        guard case .error = vm.changeState else { return XCTFail("expected action error") }
    }
}
