import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
extension CustomerAuthViewModelTests {
    // MARK: - The password the confirmation sets

    private func typeVerifyPassword(_ vm: CustomerAuthViewModel, _ password: String, confirmation: String? = nil) {
        vm.onVerifyPasswordChange(password)
        vm.onVerifyConfirmPasswordChange(confirmation ?? password)
    }

    /// Right after signing up, the confirmation sets the password the customer has just typed, so the screen
    /// asks for nothing more.
    func testRightAfterSigningUpTheConfirmationSendsThePasswordJustTyped() async {
        let vm = makeViewModel(pendingEmail: "a@b.cz", pendingPassword: "abcdefg1")
        XCTAssertFalse(vm.asksForVerifyPassword)
        vm.setVerifyCodeForTest("123456")

        await vm.confirmEmail()

        XCTAssertEqual(confirmation.lastConfirm?.email, "a@b.cz")
        XCTAssertEqual(confirmation.lastConfirm?.code, "123456")
        XCTAssertEqual(confirmation.lastConfirm?.password, "abcdefg1")
    }

    func testConfirmingLaterTheScreenAsksForThePassword() {
        XCTAssertTrue(makeViewModel(pendingEmail: "a@b.cz").asksForVerifyPassword)
    }

    func testConfirmingLaterSendsThePasswordTypedOnTheScreen() async {
        let vm = makeViewModel(pendingEmail: "a@b.cz")
        vm.setVerifyCodeForTest("123456")
        typeVerifyPassword(vm, "Chosen123")

        await vm.confirmEmail()

        XCTAssertEqual(confirmation.confirmCallCount, 1)
        XCTAssertEqual(confirmation.lastConfirm?.password, "Chosen123")
    }

    func testAVerifyPasswordOutsideTheRulesIsRefusedOnTheScreenAndNotSent() async {
        let vm = makeViewModel(pendingEmail: "a@b.cz")
        vm.setVerifyCodeForTest("123456")
        typeVerifyPassword(vm, "letters")

        await vm.confirmEmail()

        XCTAssertEqual(confirmation.confirmCallCount, 0)
        XCTAssertEqual(vm.verifyPasswordForm.passwordError, L10n.Auth.errorPasswordRules)
        XCTAssertEqual(vm.confirmState, .idle)
    }

    func testTwoDifferentVerifyPasswordsAreRefusedOnTheScreenAndNotSent() async {
        let vm = makeViewModel(pendingEmail: "a@b.cz")
        vm.setVerifyCodeForTest("123456")
        typeVerifyPassword(vm, "Chosen123", confirmation: "Chosen124")

        await vm.confirmEmail()

        XCTAssertEqual(confirmation.confirmCallCount, 0)
        XCTAssertNil(vm.verifyPasswordForm.passwordError)
        XCTAssertEqual(vm.verifyPasswordForm.confirmationError, L10n.Auth.errorPasswordsNoMatch)
    }

    func testTypingClearsTheVerifyFieldsError() async {
        let vm = makeViewModel(pendingEmail: "a@b.cz")
        vm.setVerifyCodeForTest("123456")
        await vm.confirmEmail()
        XCTAssertNotNil(vm.verifyPasswordForm.passwordError)
        XCTAssertNotNil(vm.verifyPasswordForm.confirmationError)

        typeVerifyPassword(vm, "Chosen123")

        XCTAssertNil(vm.verifyPasswordForm.passwordError)
        XCTAssertNil(vm.verifyPasswordForm.confirmationError)
    }

    /// The code confirms itself on its sixth digit only when there is a password to send, so a customer who
    /// types the code first is not shown an error for fields they have not reached yet.
    func testTheCompleteCodeWaitsForAPasswordTheScreenAsksFor() {
        let vm = makeViewModel(pendingEmail: "a@b.cz")
        vm.onVerifyCodeChange("123456")
        XCTAssertFalse(vm.isVerifyReadyToConfirm)

        typeVerifyPassword(vm, "Chosen123", confirmation: "Chosen12")
        XCTAssertFalse(vm.isVerifyReadyToConfirm)

        vm.onVerifyConfirmPasswordChange("Chosen123")
        XCTAssertTrue(vm.isVerifyReadyToConfirm)
    }

    func testTheCompleteCodeIsReadyAtOnceRightAfterSigningUp() {
        let vm = makeViewModel(pendingEmail: "a@b.cz", pendingPassword: "abcdefg1")
        vm.onVerifyCodeChange("12345")
        XCTAssertFalse(vm.isVerifyReadyToConfirm)

        vm.onVerifyCodeChange("123456")
        XCTAssertTrue(vm.isVerifyReadyToConfirm)
    }

    /// Signing in to an unconfirmed account is confirming later: no password rides the outcome, so the
    /// confirm screen asks for the one the confirmation sets.
    func testSignInToAnUnconfirmedAccountCarriesNoPassword() async {
        login.result = .success(.unverifiedEmail(email: "a@b.cz", hasToken: false))
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        vm.onSignInEmailChange("a@b.cz")
        vm.onSignInPasswordChange("secret12")

        await vm.signIn()

        XCTAssertEqual(received(), .needsEmailConfirm(email: "a@b.cz", password: nil))
    }

    /// The root view builds the confirm screen's view model from the route, so the hand-off is pinned
    /// where it happens: a router that dropped the password would still compile.
    func testTheRootHandsTheRoutesPasswordToTheConfirmScreen() throws {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(
            contentsOf: root.appendingPathComponent("CleansiaCustomer/Sources/CustomerRootView.swift"),
            encoding: .utf8
        )
        XCTAssertTrue(source.contains("case let .verifyEmail(email, password):"))
        XCTAssertTrue(source.contains("makeAuthViewModel(pendingEmail: email, pendingPassword: password)"))

        let screen = try String(
            contentsOf: root.appendingPathComponent("CleansiaCustomer/Sources/Features/Auth/EmailVerifyView.swift"),
            encoding: .utf8
        )
        XCTAssertTrue(
            screen.contains("if vm.isVerifyReadyToConfirm, !vm.confirmState.isSubmitting {"),
            "the complete code confirms without waiting for the password the screen asks for"
        )
        XCTAssertTrue(
            screen.contains("passwordForm: vm.asksForVerifyPassword ? vm.verifyPasswordForm : nil,"),
            "the password fields no longer follow whether the confirmation asks for a password"
        )
    }
}
