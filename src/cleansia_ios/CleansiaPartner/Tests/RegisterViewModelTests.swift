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
        private(set) var lastCountryId: String?
        private(set) var lastTermsAccepted: Bool?

        func register(_ request: RegisterRequest) async -> ApiResult<Bool> {
            callCount += 1
            lastArgs = (request.email, request.password, request.firstName, request.lastName, request.language)
            lastCountryId = request.countryId
            lastTermsAccepted = request.termsAccepted
            return result
        }
    }

    private final class FakeMarketClient: PartnerMarketClient, @unchecked Sendable {
        var result: ApiResult<[RegisterMarket]> = .success(RegisterMarketFixtures.two)
        private(set) var callCount = 0

        func getMarkets() async -> ApiResult<[RegisterMarket]> {
            callCount += 1
            return result
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

    private var client: FakeRegisterClient!
    private var marketClient: FakeMarketClient!
    private var settings: FakeSettings!
    private var snackbar: SnackbarController!
    private var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        client = FakeRegisterClient()
        marketClient = FakeMarketClient()
        settings = FakeSettings()
        snackbar = SnackbarController()
        cancellables = []
    }

    override func tearDown() {
        cancellables = nil
        snackbar = nil
        settings = nil
        marketClient = nil
        client = nil
        super.tearDown()
    }

    private func makeViewModel() -> RegisterViewModel {
        RegisterViewModel(
            client: client,
            marketClient: marketClient,
            settings: settings,
            snackbar: snackbar
        )
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

    /// The terms box is a hard blocker, not a hint: an unticked form never reaches the wire, so the
    /// server is never asked to record a consent nobody gave.
    func testUnacceptedTermsSetsErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        fillValid(vm)
        vm.onAcceptTermsChange(false)
        await vm.register()

        XCTAssertNotNil(vm.form.termsError)
        XCTAssertEqual(client.callCount, 0)
        XCTAssertNil(client.lastTermsAccepted)
    }

    /// The tick rides the registration itself: the server grants the employee consents in the same
    /// commit that creates the account, so nothing is parked on the device any more.
    func testASuccessfulRegistrationSendsTheTickOnTheRegistrationItself() async {
        client.result = .success(true)
        let vm = makeViewModel()
        fillValid(vm)

        await vm.register()

        XCTAssertEqual(client.lastTermsAccepted, true)
    }

    func testARejectedRegistrationSurfacesTheRefusalAndEmitsNoSuccess() async {
        client.result = .failure(ApiError(code: "user.existing_email", httpStatus: 400))
        let vm = makeViewModel()
        fillValid(vm)

        var receivedEmail: String?
        vm.registerSuccess.sink { receivedEmail = $0 }.store(in: &cancellables)

        await vm.register()

        XCTAssertNil(receivedEmail)
        XCTAssertEqual(vm.registerState, .idle)
        XCTAssertEqual(client.callCount, 1)
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
        let preferences = PreferencesModel(settings: store, languageSync: SilentLanguageSync())
        XCTAssertEqual(preferences.languageTag, "en")

        preferences.selectLanguage(id: "uk")

        let vm = RegisterViewModel(
            client: client,
            marketClient: marketClient,
            settings: store,
            snackbar: snackbar
        )
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
        let preferences = PreferencesModel(settings: store, languageSync: SilentLanguageSync())
        preferences.selectLanguage(id: "de-DE")

        let vm = RegisterViewModel(
            client: client,
            marketClient: marketClient,
            settings: store,
            snackbar: snackbar
        )
        fillValid(vm)
        await vm.register()

        XCTAssertEqual(client.lastArgs?.language, "cs")
        XCTAssertTrue(
            UserDefaultsAppSettingsStore.supportedLanguageTags.contains(client.lastArgs?.language ?? "")
        )
    }

    // MARK: - The market picker

    /// The form lists exactly what the partner host's market directory returns and preselects the
    /// row it flags as default; the cleaner is registered with that country's operating company and
    /// held to it at approval, so the two have to agree from the first write.
    func testTheDirectoryIsListedAndTheDefaultMarketIsPreselected() async {
        let vm = makeViewModel()

        await vm.loadMarkets()

        XCTAssertEqual(vm.market.markets, RegisterMarketFixtures.two)
        XCTAssertEqual(vm.market.selected, RegisterMarketFixtures.czechia)
        XCTAssertTrue(vm.market.offersChoice)
    }

    func testRegisterSendsThePreselectedDefaultMarket() async {
        let vm = makeViewModel()
        await vm.loadMarkets()
        fillValid(vm)

        await vm.register()

        XCTAssertEqual(client.lastCountryId, "cze")
    }

    func testRegisterSendsTheMarketTheCleanerPicked() async {
        let vm = makeViewModel()
        await vm.loadMarkets()
        fillValid(vm)
        vm.onMarketChange(countryId: "svk")

        await vm.register()

        XCTAssertEqual(vm.market.selected, RegisterMarketFixtures.slovakia)
        XCTAssertEqual(client.lastCountryId, "svk")
    }

    func testPickingAnUnlistedMarketKeepsTheCurrentChoice() async {
        let vm = makeViewModel()
        await vm.loadMarkets()

        vm.onMarketChange(countryId: "deu")
        vm.onMarketChange(countryId: nil)

        XCTAssertEqual(vm.market.selected, RegisterMarketFixtures.czechia)
    }

    func testWithNoDefaultFlagTheFirstRowIsPreselected() async {
        marketClient.result = .success([RegisterMarketFixtures.slovakia, RegisterMarketFixtures.germany])
        let vm = makeViewModel()

        await vm.loadMarkets()

        XCTAssertEqual(vm.market.selected, RegisterMarketFixtures.slovakia)
    }

    /// One market is no choice: the picker stays off the form and the row is still what is sent.
    func testASingleMarketOffersNoChoiceButIsStillSent() async {
        marketClient.result = .success([RegisterMarketFixtures.czechia])
        let vm = makeViewModel()
        await vm.loadMarkets()
        fillValid(vm)

        await vm.register()

        XCTAssertFalse(vm.market.offersChoice)
        XCTAssertEqual(client.lastCountryId, "cze")
    }

    /// No directory is the no-market state: the form sends nothing and the server registers the
    /// cleaner with the default market's operating company, which is what the picker would have
    /// preselected.
    func testAnUnreadableDirectorySendsNoCountryRatherThanBlockingRegistration() async {
        marketClient.result = .failure(ApiError(httpStatus: 500))
        let vm = makeViewModel()
        await vm.loadMarkets()
        fillValid(vm)

        await vm.register()

        XCTAssertEqual(vm.market, .unavailable)
        XCTAssertEqual(client.callCount, 1)
        XCTAssertNil(client.lastCountryId)
    }

    func testAnEmptyDirectoryIsTheNoMarketState() async {
        marketClient.result = .success([])
        let vm = makeViewModel()

        await vm.loadMarkets()

        XCTAssertEqual(vm.market, .unavailable)
    }

    func testAnUnreadableDirectoryIsRetriedOnTheNextAppearanceAndAHeldListIsNot() async {
        marketClient.result = .failure(ApiError(httpStatus: 500))
        let vm = makeViewModel()
        await vm.loadMarkets()
        marketClient.result = .success(RegisterMarketFixtures.two)

        await vm.loadMarkets()
        vm.onMarketChange(countryId: "svk")
        await vm.loadMarkets()

        XCTAssertEqual(marketClient.callCount, 2)
        XCTAssertEqual(vm.market.selected, RegisterMarketFixtures.slovakia)
    }

    func testTheRowLabelNamesTheCountryInTheCleanersLanguageWithItsCurrency() {
        XCTAssertEqual(RegisterMarketLabel.row(RegisterMarketFixtures.czechia, languageTag: "cs"), "Česko · CZK")
        XCTAssertEqual(RegisterMarketLabel.row(RegisterMarketFixtures.czechia, languageTag: "cs-CZ"), "Česko · CZK")
        XCTAssertEqual(RegisterMarketLabel.row(RegisterMarketFixtures.czechia, languageTag: "en"), "Czechia · CZK")
        XCTAssertEqual(RegisterMarketLabel.row(RegisterMarketFixtures.germany, languageTag: "cs"), "Germany · EUR")
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

enum RegisterMarketFixtures {
    static let czechia = RegisterMarket(
        countryId: "cze",
        isoCode: "CZE",
        name: "Czechia",
        translations: ["cs": "Česko", "sk": "Česko"],
        currencyCode: "CZK",
        isDefault: true
    )

    static let slovakia = RegisterMarket(
        countryId: "svk",
        isoCode: "SVK",
        name: "Slovakia",
        translations: ["cs": "Slovensko"],
        currencyCode: "EUR",
        isDefault: false
    )

    static let germany = RegisterMarket(
        countryId: "deu",
        isoCode: "DEU",
        name: "Germany",
        translations: [:],
        currencyCode: "EUR",
        isDefault: false
    )

    static let two = [czechia, slovakia]
}

/// For flows that never open the market picker: the directory is never read and a market-less
/// register sends no countryId, which is the shape those flows were written against.
struct UnreadMarketClient: PartnerMarketClient {
    func getMarkets() async -> ApiResult<[RegisterMarket]> {
        .success([])
    }
}
