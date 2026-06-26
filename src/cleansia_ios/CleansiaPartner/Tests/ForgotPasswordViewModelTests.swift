import CleansiaCore
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class ForgotPasswordViewModelTests: XCTestCase {
    private final class FakeResetClient: PasswordResetClient {
        var result: ApiResult<Void> = .success(())
        private(set) var callCount = 0
        private(set) var lastArgs: (email: String, language: String)?

        func forgotPassword(email: String, language: String) async -> ApiResult<Void> {
            callCount += 1
            lastArgs = (email, language)
            return result
        }
    }

    private final class FakeSettings: AppSettingsStore {
        var hasSeenOnboarding = false
        func markOnboardingSeen() {
            hasSeenOnboarding = true
        }

        var languageTag = "cs"
    }

    private var client: FakeResetClient!
    private var settings: FakeSettings!
    private var snackbar: SnackbarController!
    private var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        client = FakeResetClient()
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

    private func makeViewModel() -> ForgotPasswordViewModel {
        ForgotPasswordViewModel(client: client, settings: settings, snackbar: snackbar)
    }

    func testBlankEmailSetsErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        await vm.submit()

        XCTAssertNotNil(vm.form.emailError)
        XCTAssertEqual(client.callCount, 0)
    }

    func testInvalidEmailSetsErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        vm.onEmailChange("not-an-email")
        await vm.submit()

        XCTAssertNotNil(vm.form.emailError)
        XCTAssertEqual(client.callCount, 0)
    }

    func testValidEmailSubmitsAndEmitsRequestSuccessWithLanguage() async {
        client.result = .success(())
        settings.languageTag = "uk"
        let vm = makeViewModel()
        vm.onEmailChange("a@b.cz")

        var received = false
        vm.requestSuccess.sink { received = true }.store(in: &cancellables)

        await vm.submit()

        XCTAssertTrue(received)
        XCTAssertEqual(vm.requestState, .idle)
        XCTAssertEqual(client.callCount, 1)
        XCTAssertEqual(client.lastArgs?.email, "a@b.cz")
        XCTAssertEqual(client.lastArgs?.language, "uk")
    }

    func testFailureSnackbarsAndReturnsToIdleWithoutSuccess() async {
        client.result = .failure(ApiError(code: "network.unreachable"))
        let vm = makeViewModel()
        vm.onEmailChange("a@b.cz")

        var received = false
        vm.requestSuccess.sink { received = true }.store(in: &cancellables)

        await vm.submit()

        XCTAssertFalse(received)
        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertEqual(vm.requestState, .idle)
    }

    func testReentryGuardWhileSubmitting() async {
        let vm = makeViewModel()
        vm.onEmailChange("a@b.cz")
        vm.forceSubmittingForTest()

        await vm.submit()

        XCTAssertEqual(client.callCount, 0)
    }

    func testEmailChangeClearsError() async {
        let vm = makeViewModel()
        await vm.submit()
        XCTAssertNotNil(vm.form.emailError)

        vm.onEmailChange("a@b.cz")
        XCTAssertNil(vm.form.emailError)
    }
}
