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

        func register(_ request: RegisterRequest) async -> ApiResult<Bool> {
            callCount += 1
            lastArgs = (request.email, request.password, request.firstName, request.lastName, request.language)
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

        var receivedEmail: String?
        vm.registerSuccess.sink { receivedEmail = $0 }.store(in: &cancellables)

        await vm.register()

        // The email must ride along: PartnerRootView routes it to .verifyEmail so the user lands on
        // the confirm-email step the code was just sent to, instead of being bounced to login.
        XCTAssertEqual(receivedEmail, "jana@b.cz")
        XCTAssertEqual(vm.registerState, .idle)
        XCTAssertEqual(client.callCount, 1)
        XCTAssertEqual(client.lastArgs?.email, "jana@b.cz")
        XCTAssertEqual(client.lastArgs?.firstName, "Jana")
        XCTAssertEqual(client.lastArgs?.lastName, "Novakova")
        XCTAssertEqual(client.lastArgs?.language, "sk")
    }

    /// The point of the pre-auth language menu: what the cleaner picks on the
    /// intro screen is what the confirmation email is rendered in. Uses the real
    /// `UserDefaultsAppSettingsStore` rather than `FakeSettings` because the
    /// store is the piece that clamps — this is the whole chain from menu tap to
    /// wire value.
    func testLanguagePickedDuringOnboardingIsWhatRegisterSends() async throws {
        let suiteName = "RegisterViewModelTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }

        // A German handset: nothing supported in the device list, so the intro
        // would otherwise register "en".
        let store = UserDefaultsAppSettingsStore(defaults: defaults, preferredLanguageTags: { ["de-DE"] })
        let preferences = PreferencesModel(settings: store)
        XCTAssertEqual(preferences.languageTag, "en")

        preferences.selectLanguage(id: "uk")

        let vm = RegisterViewModel(client: client, settings: store, snackbar: snackbar)
        fillValid(vm)
        await vm.register()

        XCTAssertEqual(client.lastArgs?.language, "uk")
    }

    /// The registration-failure guard, end to end. A tag outside the five is
    /// never allowed to reach the API — `LanguageValidator` would reject it with
    /// `language.not_supported` and fail the whole signup, not just the email.
    func testUnsupportedLanguageNeverReachesTheApi() async throws {
        let suiteName = "RegisterViewModelTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }

        let store = UserDefaultsAppSettingsStore(defaults: defaults, preferredLanguageTags: { ["cs-CZ"] })
        let preferences = PreferencesModel(settings: store)
        preferences.selectLanguage(id: "de-DE")

        let vm = RegisterViewModel(client: client, settings: store, snackbar: snackbar)
        fillValid(vm)
        await vm.register()

        XCTAssertEqual(client.lastArgs?.language, "cs")
        XCTAssertTrue(
            UserDefaultsAppSettingsStore.supportedLanguageTags.contains(client.lastArgs?.language ?? "")
        )
    }

    func testRegisterFailureSnackbarsAndReturnsToIdleWithoutSuccess() async {
        client.result = .failure(ApiError(code: "network.unreachable"))
        let vm = makeViewModel()
        fillValid(vm)

        var received = false
        vm.registerSuccess.sink { _ in received = true }.store(in: &cancellables)

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
