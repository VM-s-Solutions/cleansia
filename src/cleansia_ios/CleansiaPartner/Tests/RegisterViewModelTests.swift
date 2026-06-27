import CleansiaCore
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class RegisterViewModelTests: XCTestCase {
    private final class FakeRegisterClient: RegistrationAuthClient {
        var result: ApiResult<Bool> = .success(true)
        private(set) var callCount = 0
        private(set) var lastArgs: (
            email: String,
            password: String,
            firstName: String,
            lastName: String,
            language: String
        )?

        func register(
            email: String,
            password: String,
            firstName: String,
            lastName: String,
            language: String
        ) async -> ApiResult<Bool> {
            callCount += 1
            lastArgs = (email, password, firstName, lastName, language)
            return result
        }
    }

    private final class FakeSettings: AppSettingsStore {
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

    private var client: FakeRegisterClient!
    private var settings: FakeSettings!
    private var snackbar: SnackbarController!
    private var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        client = FakeRegisterClient()
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

    private func makeViewModel() -> RegisterViewModel {
        RegisterViewModel(client: client, settings: settings, snackbar: snackbar)
    }

    private func fillValid(_ vm: RegisterViewModel) {
        vm.onFirstNameChange("Jana")
        vm.onLastNameChange("Novakova")
        vm.onEmailChange("jana@b.cz")
        vm.onPasswordChange("abcdefg1")
        vm.onConfirmPasswordChange("abcdefg1")
        vm.onAcceptTermsChange(true)
    }

    func testBlankFirstNameSetsErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        fillValid(vm)
        vm.onFirstNameChange("")
        await vm.register()

        XCTAssertNotNil(vm.form.firstNameError)
        XCTAssertEqual(client.callCount, 0)
    }

    func testBlankLastNameSetsErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        fillValid(vm)
        vm.onLastNameChange("")
        await vm.register()

        XCTAssertNotNil(vm.form.lastNameError)
        XCTAssertEqual(client.callCount, 0)
    }

    func testBlankEmailSetsErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        fillValid(vm)
        vm.onEmailChange("")
        await vm.register()

        XCTAssertNotNil(vm.form.emailError)
        XCTAssertEqual(client.callCount, 0)
    }

    func testInvalidEmailSetsErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        fillValid(vm)
        vm.onEmailChange("not-an-email")
        await vm.register()

        XCTAssertNotNil(vm.form.emailError)
        XCTAssertEqual(client.callCount, 0)
    }

    func testWeakPasswordSetsErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        fillValid(vm)
        vm.onPasswordChange("short")
        vm.onConfirmPasswordChange("short")
        await vm.register()

        XCTAssertNotNil(vm.form.passwordError)
        XCTAssertEqual(client.callCount, 0)
    }

    func testMismatchedPasswordsSetsErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        fillValid(vm)
        vm.onConfirmPasswordChange("abcdefg2")
        await vm.register()

        XCTAssertNotNil(vm.form.confirmPasswordError)
        XCTAssertEqual(client.callCount, 0)
    }

    func testUnacceptedTermsSetsErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        fillValid(vm)
        vm.onAcceptTermsChange(false)
        await vm.register()

        XCTAssertNotNil(vm.form.termsError)
        XCTAssertEqual(client.callCount, 0)
    }

    func testValidFormSubmitsAndEmitsRegisterSuccess() async {
        client.result = .success(true)
        settings.languageTag = "sk"
        let vm = makeViewModel()
        fillValid(vm)

        var received = false
        vm.registerSuccess.sink { received = true }.store(in: &cancellables)

        await vm.register()

        XCTAssertTrue(received)
        XCTAssertEqual(vm.registerState, .idle)
        XCTAssertEqual(client.callCount, 1)
        XCTAssertEqual(client.lastArgs?.email, "jana@b.cz")
        XCTAssertEqual(client.lastArgs?.firstName, "Jana")
        XCTAssertEqual(client.lastArgs?.lastName, "Novakova")
        XCTAssertEqual(client.lastArgs?.language, "sk")
    }

    func testRegisterFailureSnackbarsAndReturnsToIdleWithoutSuccess() async {
        client.result = .failure(ApiError(code: "network.unreachable"))
        let vm = makeViewModel()
        fillValid(vm)

        var received = false
        vm.registerSuccess.sink { received = true }.store(in: &cancellables)

        await vm.register()

        XCTAssertFalse(received)
        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertEqual(vm.registerState, .idle)
    }

    func testReentryGuardWhileSubmitting() async {
        let vm = makeViewModel()
        fillValid(vm)
        vm.forceSubmittingForTest()

        await vm.register()

        XCTAssertEqual(client.callCount, 0)
    }

    func testFieldChangeClearsThatFieldError() async {
        let vm = makeViewModel()
        await vm.register()
        XCTAssertNotNil(vm.form.firstNameError)

        vm.onFirstNameChange("Jana")
        XCTAssertNil(vm.form.firstNameError)
    }

    func testPasswordRuleFlagsTrackInput() {
        let vm = makeViewModel()
        vm.onPasswordChange("abc")
        XCTAssertFalse(vm.form.passwordHasMinLength)
        XCTAssertTrue(vm.form.passwordHasLetter)
        XCTAssertFalse(vm.form.passwordHasNumber)

        vm.onPasswordChange("abcdefg1")
        XCTAssertTrue(vm.form.passwordHasMinLength)
        XCTAssertTrue(vm.form.passwordHasLetter)
        XCTAssertTrue(vm.form.passwordHasNumber)

        vm.onConfirmPasswordChange("abcdefg1")
        XCTAssertTrue(vm.form.passwordsMatch)
    }

    func testIsValidRequiresEveryField() {
        let vm = makeViewModel()
        XCTAssertFalse(vm.form.isValid)
        fillValid(vm)
        XCTAssertTrue(vm.form.isValid)
    }
}
