import CleansiaCore
import Combine
import XCTest
@testable import CleansiaPartner

private struct ConfirmCall {
    let email: String
    let code: String
    let password: String
}

@MainActor
final class ConfirmEmailViewModelTests: XCTestCase {
    private final class FakeConfirmClient: EmailConfirmationClient {
        var confirmResult: ApiResult<LoginOutcome> = .success(.authenticated)
        var resendResult: ApiResult<Bool> = .success(true)
        private(set) var confirmCallCount = 0
        private(set) var resendCallCount = 0
        private(set) var lastResendArgs: (email: String, language: String)?
        private(set) var lastConfirm: ConfirmCall?

        func confirmEmail(email: String, code: String, password: String) async -> ApiResult<LoginOutcome> {
            confirmCallCount += 1
            lastConfirm = ConfirmCall(email: email, code: code, password: password)
            return confirmResult
        }

        func resendConfirmation(email: String, language: String) async -> ApiResult<Bool> {
            resendCallCount += 1
            lastResendArgs = (email, language)
            return resendResult
        }
    }

    private final class FakeSettings: AppSettingsStore {
        private(set) var answeredPrompts: Set<String> = []
        func hasAnsweredPrompt(_ prompt: String, userId: String) -> Bool {
            answeredPrompts.contains("\(prompt)/\(userId)")
        }

        func markPromptAnswered(_ prompt: String, userId: String) {
            answeredPrompts.insert("\(prompt)/\(userId)")
        }

        var hasSeenOnboarding = false
        func markOnboardingSeen() {
            hasSeenOnboarding = true
        }

        var languageTag = "cs"

        var persistedLanguageTag: String?

        func setLanguage(_ tag: String) {
            languageTag = tag
            persistedLanguageTag = tag
        }

        func clearLanguage() {
            persistedLanguageTag = nil
        }

        var theme: Theme = .system
        func setTheme(_ theme: Theme) {
            self.theme = theme
        }
    }

    private var client: FakeConfirmClient!
    private var settings: FakeSettings!
    private var snackbar: SnackbarController!
    private var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        client = FakeConfirmClient()
        settings = FakeSettings()
        snackbar = SnackbarController()
        cancellables = []
    }

    override func tearDown() {
        cancellables = nil
        snackbar = nil
        settings = nil
        client = nil
        super.tearDown()
    }

    private func makeViewModel(
        email: String? = "a@b.cz",
        registeredPassword: String? = "abcdefg1"
    ) -> ConfirmEmailViewModel {
        ConfirmEmailViewModel(
            email: email,
            registeredPassword: registeredPassword,
            client: client,
            settings: settings,
            snackbar: snackbar
        )
    }

    private func typePassword(_ vm: ConfirmEmailViewModel, _ password: String, confirmation: String? = nil) {
        vm.onPasswordChange(password)
        vm.onConfirmPasswordChange(confirmation ?? password)
    }

    func testCodeChangeFiltersToDigitsAndCapsAtSix() {
        let vm = makeViewModel()
        vm.onCodeChange("12ab34cd567890")
        XCTAssertEqual(vm.code, "123456")
    }

    func testConfirmWithFewerThanSixDigitsDoesNotSubmit() async {
        let vm = makeViewModel()
        vm.onCodeChange("123")
        await vm.confirmEmail()
        XCTAssertEqual(client.confirmCallCount, 0)
    }

    func testConfirmWithSixDigitsSubmits() async {
        let vm = makeViewModel()
        vm.onCodeChange("123456")
        await vm.confirmEmail()
        XCTAssertEqual(client.confirmCallCount, 1)
    }

    func testAuthenticatedConfirmEmitsConfirmSuccess() async {
        client.confirmResult = .success(.authenticated)
        let vm = makeViewModel()
        vm.onCodeChange("123456")

        var received = false
        vm.confirmSuccess.sink { received = true }.store(in: &cancellables)

        await vm.confirmEmail()

        XCTAssertTrue(received)
        XCTAssertEqual(vm.confirmState, .idle)
    }

    func testEmptyTokenConfirmShowsGenericErrorAndDoesNotEnterApp() async {
        client.confirmResult = .success(.unverifiedEmail(email: "a@b.cz", hasToken: false))
        let vm = makeViewModel()
        vm.onCodeChange("123456")

        var received = false
        vm.confirmSuccess.sink { received = true }.store(in: &cancellables)

        await vm.confirmEmail()

        XCTAssertFalse(received)
        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertEqual(vm.confirmState, .idle)
    }

    func testConfirmFailureSnackbarsWithoutSuccess() async {
        client.confirmResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeViewModel()
        vm.onCodeChange("123456")

        var received = false
        vm.confirmSuccess.sink { received = true }.store(in: &cancellables)

        await vm.confirmEmail()

        XCTAssertFalse(received)
        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    func testResendWithNilEmailShowsGenericErrorAndDoesNotCall() async {
        let vm = makeViewModel(email: nil)
        XCTAssertFalse(vm.canResend)

        await vm.resendCode()

        XCTAssertEqual(client.resendCallCount, 0)
        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    func testResendWithBlankEmailDoesNotCall() async {
        let vm = makeViewModel(email: "   ")

        await vm.resendCode()

        XCTAssertEqual(client.resendCallCount, 0)
    }

    func testResendSuccessSendsLanguageAndShowsSuccessSnackbar() async {
        client.resendResult = .success(true)
        settings.languageTag = "sk"
        let vm = makeViewModel(email: "a@b.cz")

        await vm.resendCode()

        XCTAssertEqual(client.resendCallCount, 1)
        XCTAssertEqual(client.lastResendArgs?.email, "a@b.cz")
        XCTAssertEqual(client.lastResendArgs?.language, "sk")
        XCTAssertEqual(snackbar.current?.severity, .success)
    }

    func testResendFailureSnackbarsError() async {
        client.resendResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeViewModel(email: "a@b.cz")

        await vm.resendCode()

        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    // MARK: - The password the confirmation sets

    /// Right after registering, the confirmation sets the password the cleaner has just typed, so the screen
    /// asks for nothing more.
    func testRightAfterRegisteringTheConfirmationSendsThePasswordJustTyped() async {
        let vm = makeViewModel(registeredPassword: "abcdefg1")
        XCTAssertFalse(vm.asksForPassword)
        vm.onCodeChange("123456")

        await vm.confirmEmail()

        XCTAssertEqual(client.lastConfirm?.email, "a@b.cz")
        XCTAssertEqual(client.lastConfirm?.code, "123456")
        XCTAssertEqual(client.lastConfirm?.password, "abcdefg1")
    }

    func testConfirmingLaterTheScreenAsksForThePassword() {
        XCTAssertTrue(makeViewModel(registeredPassword: nil).asksForPassword)
    }

    func testConfirmingLaterSendsThePasswordTypedOnTheScreen() async {
        let vm = makeViewModel(registeredPassword: nil)
        vm.onCodeChange("123456")
        typePassword(vm, "Chosen123")

        await vm.confirmEmail()

        XCTAssertEqual(client.confirmCallCount, 1)
        XCTAssertEqual(client.lastConfirm?.password, "Chosen123")
    }

    func testAPasswordOutsideTheRulesIsRefusedOnTheScreenAndNotSent() async {
        let vm = makeViewModel(registeredPassword: nil)
        vm.onCodeChange("123456")
        typePassword(vm, "letters")

        await vm.confirmEmail()

        XCTAssertEqual(client.confirmCallCount, 0)
        XCTAssertEqual(vm.passwordForm.passwordError, L10n.Register.errorPasswordRules)
        XCTAssertEqual(vm.confirmState, .idle)
    }

    func testTwoDifferentPasswordsAreRefusedOnTheScreenAndNotSent() async {
        let vm = makeViewModel(registeredPassword: nil)
        vm.onCodeChange("123456")
        typePassword(vm, "Chosen123", confirmation: "Chosen124")

        await vm.confirmEmail()

        XCTAssertEqual(client.confirmCallCount, 0)
        XCTAssertNil(vm.passwordForm.passwordError)
        XCTAssertEqual(vm.passwordForm.confirmationError, L10n.Register.errorPasswordsNoMatch)
    }

    func testTypingClearsTheFieldsError() async {
        let vm = makeViewModel(registeredPassword: nil)
        vm.onCodeChange("123456")
        await vm.confirmEmail()
        XCTAssertNotNil(vm.passwordForm.passwordError)
        XCTAssertNotNil(vm.passwordForm.confirmationError)

        typePassword(vm, "Chosen123")

        XCTAssertNil(vm.passwordForm.passwordError)
        XCTAssertNil(vm.passwordForm.confirmationError)
    }

    /// The code confirms itself on its sixth digit only when there is a password to send, so a cleaner who
    /// types the code first is not shown an error for fields they have not reached yet.
    func testTheCompleteCodeWaitsForAPasswordTheScreenAsksFor() {
        let vm = makeViewModel(registeredPassword: nil)
        vm.onCodeChange("123456")
        XCTAssertFalse(vm.isReadyToConfirm)

        typePassword(vm, "Chosen123", confirmation: "Chosen12")
        XCTAssertFalse(vm.isReadyToConfirm)

        vm.onConfirmPasswordChange("Chosen123")
        XCTAssertTrue(vm.isReadyToConfirm)
    }

    func testTheCompleteCodeIsReadyAtOnceRightAfterRegistering() {
        let vm = makeViewModel(registeredPassword: "abcdefg1")
        vm.onCodeChange("12345")
        XCTAssertFalse(vm.isReadyToConfirm)

        vm.onCodeChange("123456")
        XCTAssertTrue(vm.isReadyToConfirm)
    }

    /// The screen confirms the complete code through the readiness above, never on the sixth digit alone.
    func testTheScreenConfirmsTheCompleteCodeOnlyWhenReady() throws {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let screen = try String(
            contentsOf: root.appendingPathComponent("CleansiaPartner/Sources/Features/Auth/ConfirmEmailView.swift"),
            encoding: .utf8
        )
        XCTAssertTrue(screen.contains("if vm.isReadyToConfirm, !vm.confirmState.isSubmitting {"))
    }

    func testConfirmAndResendActionStatesAreIndependent() async {
        let vm = makeViewModel(email: "a@b.cz")
        vm.onCodeChange("123456")

        await vm.confirmEmail()
        XCTAssertEqual(vm.confirmState, .idle)
        XCTAssertEqual(vm.resendState, .idle)

        await vm.resendCode()
        XCTAssertEqual(vm.confirmState, .idle)
        XCTAssertEqual(vm.resendState, .idle)

        XCTAssertEqual(client.confirmCallCount, 1)
        XCTAssertEqual(client.resendCallCount, 1)
    }
}
