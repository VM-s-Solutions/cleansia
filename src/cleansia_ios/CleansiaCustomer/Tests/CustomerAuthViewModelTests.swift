import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
final class CustomerAuthViewModelTests: XCTestCase {
    private var login: FakeLoginClient!
    private var registration: FakeRegistrationClient!
    private var confirmation: FakeEmailConfirmationClient!
    private var passwordReset: FakePasswordResetClient!
    private var settings: FakeAppSettingsStore!
    private var snackbar: SnackbarController!
    private var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        login = FakeLoginClient()
        registration = FakeRegistrationClient()
        confirmation = FakeEmailConfirmationClient()
        passwordReset = FakePasswordResetClient()
        settings = FakeAppSettingsStore()
        snackbar = SnackbarController()
        cancellables = []
    }

    override func tearDown() {
        cancellables = nil
        snackbar = nil
        settings = nil
        passwordReset = nil
        confirmation = nil
        registration = nil
        login = nil
        super.tearDown()
    }

    private func makeViewModel(pendingEmail: String? = nil) -> CustomerAuthViewModel {
        CustomerAuthViewModel(
            loginClient: login,
            registrationClient: registration,
            emailConfirmationClient: confirmation,
            passwordResetClient: passwordReset,
            settings: settings,
            snackbar: snackbar,
            pendingEmail: pendingEmail
        )
    }

    private func collectOutcome(_ vm: CustomerAuthViewModel) -> () -> AuthOutcome? {
        var received: AuthOutcome?
        vm.outcome.sink { received = $0 }.store(in: &cancellables)
        return { received }
    }

    func testSignInAuthenticatedEmitsSignedIn() async {
        login.result = .success(.authenticated)
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        vm.onSignInEmailChange("a@b.cz")
        vm.onSignInPasswordChange("secret")

        await vm.signIn()

        XCTAssertEqual(received(), .signedIn)
        XCTAssertEqual(vm.signInState, .idle)
    }

    func testSignInUnverifiedEmitsNeedsEmailConfirmCarryingEmail() async {
        login.result = .success(.unverifiedEmail(email: "a@b.cz", hasToken: true))
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        vm.onSignInEmailChange("a@b.cz")
        vm.onSignInPasswordChange("secret")

        await vm.signIn()

        XCTAssertEqual(received(), .needsEmailConfirm(email: "a@b.cz"))
    }

    func testSignInEmptyTokenUnverifiedRoutesToVerifyNotError() async throws {
        login.result = .success(.unverifiedEmail(email: "a@b.cz", hasToken: false))
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        vm.onSignInEmailChange("a@b.cz")
        vm.onSignInPasswordChange("secret")

        await vm.signIn()

        XCTAssertEqual(received(), .needsEmailConfirm(email: "a@b.cz"))
        XCTAssertNil(snackbar.current)
        let outcome = try XCTUnwrap(received())
        XCTAssertEqual(CustomerRootView.Route.afterAuth(outcome), .verifyEmail(email: "a@b.cz"))
    }

    func testSignInBlankFieldsDoNotSubmit() async {
        let vm = makeViewModel()
        await vm.signIn()

        XCTAssertNotNil(vm.signInForm.emailError)
        XCTAssertNotNil(vm.signInForm.passwordError)
        XCTAssertEqual(login.callCount, 0)
    }

    func testSignInFailureSnackbarsAndEmitsNothing() async {
        login.result = .failure(ApiError(code: "network.unreachable"))
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        vm.onSignInEmailChange("a@b.cz")
        vm.onSignInPasswordChange("secret")

        await vm.signIn()

        XCTAssertNil(received())
        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    func testSignUpSuccessEmitsNeedsEmailConfirmCarryingFormEmail() async {
        registration.result = .success(true)
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        vm.onFirstNameChange("Jana")
        vm.onLastNameChange("Nováková")
        vm.onSignUpEmailChange("jana@b.cz")
        vm.onSignUpPasswordChange("abcdefg1")
        vm.onConfirmPasswordChange("abcdefg1")

        await vm.signUp()

        XCTAssertEqual(received(), .needsEmailConfirm(email: "jana@b.cz"))
        XCTAssertEqual(registration.callCount, 1)
    }

    func testSignUpEnforcesPasswordPolicy() async {
        let vm = makeViewModel()
        vm.onFirstNameChange("Jana")
        vm.onLastNameChange("Nováková")
        vm.onSignUpEmailChange("jana@b.cz")
        vm.onSignUpPasswordChange("short")
        vm.onConfirmPasswordChange("short")

        await vm.signUp()

        XCTAssertNotNil(vm.signUpForm.passwordError)
        XCTAssertEqual(registration.callCount, 0)
    }

    func testSignUpRequiresMatchingPasswords() async {
        let vm = makeViewModel()
        vm.onFirstNameChange("Jana")
        vm.onLastNameChange("Nováková")
        vm.onSignUpEmailChange("jana@b.cz")
        vm.onSignUpPasswordChange("abcdefg1")
        vm.onConfirmPasswordChange("abcdefg2")

        await vm.signUp()

        XCTAssertNotNil(vm.signUpForm.confirmPasswordError)
        XCTAssertEqual(registration.callCount, 0)
    }

    func testConfirmEmailAuthenticatedEmitsSignedIn() async {
        confirmation.confirmResult = .success(.authenticated)
        let vm = makeViewModel(pendingEmail: "a@b.cz")
        let received = collectOutcome(vm)
        vm.setVerifyCodeForTest("123456")

        await vm.confirmEmail()

        XCTAssertEqual(received(), .signedIn)
    }

    func testConfirmEmailUnverifiedShowsErrorAndEmitsNothing() async {
        confirmation.confirmResult = .success(.unverifiedEmail(email: "a@b.cz", hasToken: false))
        let vm = makeViewModel(pendingEmail: "a@b.cz")
        let received = collectOutcome(vm)
        vm.setVerifyCodeForTest("123456")

        await vm.confirmEmail()

        XCTAssertNil(received())
        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    func testResendUsesThreadedEmailAndLanguage() async {
        confirmation.resendResult = .success(true)
        settings.languageTag = "cs"
        let vm = makeViewModel(pendingEmail: "a@b.cz")

        await vm.resendCode()

        XCTAssertEqual(confirmation.lastResendArgs?.email, "a@b.cz")
        XCTAssertEqual(confirmation.lastResendArgs?.language, "cs")
        XCTAssertEqual(snackbar.current?.severity, .success)
    }

    func testResendWithoutEmailDoesNotCallAndShowsError() async {
        let vm = makeViewModel(pendingEmail: nil)

        await vm.resendCode()

        XCTAssertEqual(confirmation.resendCallCount, 0)
        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    func testCanResendReflectsPendingEmailPresence() {
        XCTAssertTrue(makeViewModel(pendingEmail: "a@b.cz").canResend)
        XCTAssertFalse(makeViewModel(pendingEmail: nil).canResend)
        XCTAssertFalse(makeViewModel(pendingEmail: "   ").canResend)
    }

    func testForgotPasswordSuccessEmitsPasswordReset() async throws {
        passwordReset.result = .success(())
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        vm.onForgotEmailChange("a@b.cz")

        await vm.requestPasswordReset()

        XCTAssertEqual(received(), .passwordReset)
        XCTAssertEqual(snackbar.current?.severity, .success)
        let outcome = try XCTUnwrap(received())
        XCTAssertEqual(CustomerRootView.Route.afterAuth(outcome), .login)
    }

    func testForgotPasswordInvalidEmailDoesNotSubmit() async {
        let vm = makeViewModel()
        vm.onForgotEmailChange("not-an-email")

        await vm.requestPasswordReset()

        XCTAssertNotNil(vm.forgotForm.emailError)
        XCTAssertEqual(passwordReset.callCount, 0)
    }

    func testSignInReentryGuardWhileSubmitting() async {
        let vm = makeViewModel()
        vm.onSignInEmailChange("a@b.cz")
        vm.onSignInPasswordChange("secret")
        vm.forceSignInSubmittingForTest()

        await vm.signIn()

        XCTAssertEqual(login.callCount, 0)
    }

    func testRouterMapsEverySignInOutcome() {
        XCTAssertEqual(CustomerRootView.Route.afterAuth(.signedIn), .home)
        XCTAssertEqual(
            CustomerRootView.Route.afterAuth(.needsEmailConfirm(email: "a@b.cz")),
            .verifyEmail(email: "a@b.cz")
        )
        XCTAssertEqual(CustomerRootView.Route.afterAuth(.passwordReset), .login)
    }
}

private final class FakeLoginClient: LoginClient {
    var result: ApiResult<LoginOutcome> = .success(.authenticated)
    private(set) var callCount = 0

    func login(email _: String, password _: String, rememberMe _: Bool) async -> ApiResult<LoginOutcome> {
        callCount += 1
        return result
    }
}

private final class FakeRegistrationClient: RegistrationAuthClient {
    var result: ApiResult<Bool> = .success(true)
    private(set) var callCount = 0
    private(set) var lastLanguage: String?

    func register(
        email _: String,
        password _: String,
        firstName _: String,
        lastName _: String,
        language: String
    ) async -> ApiResult<Bool> {
        callCount += 1
        lastLanguage = language
        return result
    }
}

private final class FakeEmailConfirmationClient: EmailConfirmationClient {
    var confirmResult: ApiResult<LoginOutcome> = .success(.authenticated)
    var resendResult: ApiResult<Bool> = .success(true)
    private(set) var resendCallCount = 0
    private(set) var lastResendArgs: (email: String, language: String)?

    func confirmEmail(code _: String) async -> ApiResult<LoginOutcome> {
        confirmResult
    }

    func resendConfirmation(email: String, language: String) async -> ApiResult<Bool> {
        resendCallCount += 1
        lastResendArgs = (email, language)
        return resendResult
    }
}

private final class FakePasswordResetClient: PasswordResetClient {
    var result: ApiResult<Void> = .success(())
    private(set) var callCount = 0

    func forgotPassword(email _: String, language _: String) async -> ApiResult<Void> {
        callCount += 1
        return result
    }
}

private final class FakeAppSettingsStore: AppSettingsStore {
    var hasSeenOnboarding = false
    var languageTag = "en"
    var persistedLanguageTag: String?
    var theme: Theme = .system

    func markOnboardingSeen() {
        hasSeenOnboarding = true
    }

    func setLanguage(_ tag: String) {
        languageTag = tag
        persistedLanguageTag = tag
    }

    func clearLanguage() {
        persistedLanguageTag = nil
    }

    func setTheme(_ theme: Theme) {
        self.theme = theme
    }
}
