import CleansiaCore
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class LoginViewModelTests: XCTestCase {
    private final class FakeLoginClient: LoginClient {
        var result: ApiResult<LoginOutcome> = .success(.authenticated)
        private(set) var callCount = 0
        private(set) var lastArgs: (email: String, password: String, rememberMe: Bool)?

        func login(email: String, password: String, rememberMe: Bool) async -> ApiResult<LoginOutcome> {
            callCount += 1
            lastArgs = (email, password, rememberMe)
            return result
        }
    }

    private var client: FakeLoginClient!
    private var snackbar: SnackbarController!
    private var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        client = FakeLoginClient()
        snackbar = SnackbarController()
        cancellables = []
    }

    override func tearDown() {
        cancellables = nil
        snackbar = nil
        client = nil
        super.tearDown()
    }

    private func makeViewModel() -> LoginViewModel {
        LoginViewModel(loginClient: client, snackbar: snackbar)
    }

    func testBlankEmailSetsFieldErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        vm.onPasswordChange("secret")
        await vm.login()

        XCTAssertNotNil(vm.form.emailError)
        XCTAssertEqual(vm.loginState, .idle)
        XCTAssertEqual(client.callCount, 0)
    }

    func testInvalidEmailFormatSetsFieldErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        vm.onEmailChange("not-an-email")
        vm.onPasswordChange("secret")
        await vm.login()

        XCTAssertNotNil(vm.form.emailError)
        XCTAssertEqual(client.callCount, 0)
    }

    func testBlankPasswordSetsFieldErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        vm.onEmailChange("a@b.com")
        await vm.login()

        XCTAssertNotNil(vm.form.passwordError)
        XCTAssertEqual(client.callCount, 0)
    }

    func testSuccessfulLoginEmitsSuccessEffectAndReturnsToIdle() async {
        client.result = .success(.authenticated)
        let vm = makeViewModel()
        vm.onEmailChange("a@b.com")
        vm.onPasswordChange("secret")

        var received: LoginSuccess?
        vm.loginSuccess
            .sink { received = $0 }
            .store(in: &cancellables)

        await vm.login()

        XCTAssertEqual(received?.requiresEmailConfirmation, false)
        XCTAssertEqual(vm.loginState, .idle)
        XCTAssertEqual(client.lastArgs?.rememberMe, true)
    }

    func testUnverifiedEmailLoginFlagsRequiresEmailConfirmation() async {
        client.result = .success(.unverifiedEmail(email: "a@b.com", hasToken: false))
        let vm = makeViewModel()
        vm.onEmailChange("a@b.com")
        vm.onPasswordChange("secret")

        var received: LoginSuccess?
        vm.loginSuccess
            .sink { received = $0 }
            .store(in: &cancellables)

        await vm.login()

        XCTAssertEqual(received?.requiresEmailConfirmation, true)
    }

    func testLoginFailureSnackbarsAndReturnsToIdleWithoutSuccessEffect() async {
        client.result = .failure(ApiError(code: "network.unreachable"))
        let vm = makeViewModel()
        vm.onEmailChange("a@b.com")
        vm.onPasswordChange("secret")

        var received: LoginSuccess?
        vm.loginSuccess
            .sink { received = $0 }
            .store(in: &cancellables)

        await vm.login()

        XCTAssertNil(received)
        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertEqual(vm.loginState, .idle)
    }

    func testReentryGuardWhileSubmitting() async {
        let vm = makeViewModel()
        vm.onEmailChange("a@b.com")
        vm.onPasswordChange("secret")
        vm.forceSubmittingForTest()

        await vm.login()

        XCTAssertEqual(client.callCount, 0)
    }

    func testEmailChangeClearsEmailError() async {
        let vm = makeViewModel()
        await vm.login()
        XCTAssertNotNil(vm.form.emailError)

        vm.onEmailChange("a@b.com")
        XCTAssertNil(vm.form.emailError)
    }
}
