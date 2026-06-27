import CleansiaCore
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class ConfirmEmailViewModelTests: XCTestCase {
    private final class FakeConfirmClient: EmailConfirmationClient {
        var confirmResult: ApiResult<LoginOutcome> = .success(.authenticated)
        var resendResult: ApiResult<Bool> = .success(true)
        private(set) var confirmCallCount = 0
        private(set) var resendCallCount = 0
        private(set) var lastResendArgs: (email: String, language: String)?

        func confirmEmail(code _: String) async -> ApiResult<LoginOutcome> {
            confirmCallCount += 1
            return confirmResult
        }

        func resendConfirmation(email: String, language: String) async -> ApiResult<Bool> {
            resendCallCount += 1
            lastResendArgs = (email, language)
            return resendResult
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

    private func makeViewModel(email: String? = "a@b.cz") -> ConfirmEmailViewModel {
        ConfirmEmailViewModel(email: email, client: client, settings: settings, snackbar: snackbar)
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
