import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// Signup consent, registration through delivery, over the app's REAL generated `CustomerGdprAPI` —
/// every assertion is on the `GrantConsentCommand` instances that reach the wire, because the one
/// mistake here that produces a false legal record is a command carrying the wrong `ConsentType`,
/// and a "was it called" check passes on that just as happily.
///
/// Customer registration produces NO session: `api/Auth/Register` answers a bare boolean and the
/// customer is routed to email verification, while `GrantConsent` needs an authenticated caller. So
/// the tick is parked at registration and delivered at the first token response in which the server
/// itself names the same account.
@MainActor
final class SignupConsentFlowTests: XCTestCase {
    private var suiteName: String!
    private var defaults: UserDefaults!
    private var tokenStore: FlowTokenStore!
    private var spine: AuthApiClient!
    private var signupConsent: SignupConsentRepository!

    private let email = "ada@example.com"

    override func setUpWithError() throws {
        try super.setUpWithError()
        ConsentWireStub.reset()
        suiteName = "SignupConsentFlowTests.\(UUID().uuidString)"
        defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        tokenStore = FlowTokenStore()

        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [ConsentWireStub.self]
        let session = URLSession(configuration: config)
        let headerAdapter = HeaderAdapter(deviceIdProvider: FlowDeviceId(), anonymousAllowList: .customer)

        signupConsent = SignupConsentRepository(
            store: UserDefaultsSignupConsentStore(defaults: defaults),
            client: CustomerSignupConsentClient()
        )
        spine = try AuthApiClient(
            apiBaseURL: XCTUnwrap(URL(string: "https://api.test/")),
            tokenStore: tokenStore,
            headerAdapter: headerAdapter,
            sessionScopedCaches: SessionScopedCacheRegistry(),
            registerEndpoint: .customer,
            signupConsent: signupConsent,
            authedSession: session,
            noAuthSession: session
        )
        CustomerGeneratedAuth.install(
            bridge: GeneratedClientAuthBridge(
                headerAdapter: headerAdapter,
                tokenStore: tokenStore,
                sessionRefresher: SessionRefresher(
                    tokenStore: tokenStore,
                    refreshClient: NoRefreshClient(),
                    sessionManager: SessionManager(),
                    sessionScopedCaches: SessionScopedCacheRegistry()
                ),
                session: session
            ),
            basePath: "https://api.test"
        )
    }

    override func tearDown() {
        ConsentWireStub.reset()
        defaults.removePersistentDomain(forName: suiteName)
        super.tearDown()
    }

    func testATickedBoxGrantsTermsOfServiceAndPrivacyPolicyAndNothingElse() async {
        await signUp()

        await signIn()

        XCTAssertEqual(ConsentWireStub.grantedTypes, [._0, ._1])
    }

    func testARejectedRegistrationGrantsNothing() async {
        ConsentWireStub.registerResponse = { (400, Data(#"{"errors":{"Email":"user.existing_email"}}"#.utf8)) }
        await signUp()

        await signIn()

        XCTAssertEqual(ConsentWireStub.grantedTypes, [])
    }

    /// The literal body `GrantConsent`'s failure arm produces: `CreateProblemDetails` keys the
    /// `errors` bag by `Error.Code` (the offending field) and values it with `Error.Message`.
    func testADuplicateRefusalCountsAsDeliveredAndIsNotRetried() async {
        ConsentWireStub.grantResponse = {
            (400, Data(#"""
            {"title":"Bad Request","type":"ConsentType","detail":"gdpr.consent_already_granted",
             "status":400,"errors":{"ConsentType":"gdpr.consent_already_granted"}}
            """#.utf8))
        }
        await signUp()

        await signIn()
        await signIn()

        XCTAssertEqual(ConsentWireStub.grantedTypes, [._0, ._1])
    }

    /// The same refusal with the two `Error` slots swapped — the shape that shipped before the fix.
    /// The dot code sat in the bag KEY and the VALUE every client resolves was prose, so the refusal
    /// was unrecognisable and the tick was re-sent every session.
    func testADuplicateRefusalWithSwappedSlotsIsNotRecognised() async {
        ConsentWireStub.grantResponse = {
            (400, Data(#"""
            {"title":"Bad Request","type":"gdpr.consent_already_granted","detail":"Consent already granted",
             "status":400,"errors":{"gdpr.consent_already_granted":"Consent already granted"}}
            """#.utf8))
        }
        await signUp()

        await signIn()
        await signIn()

        XCTAssertEqual(ConsentWireStub.grantedTypes, [._0, ._1, ._0, ._1])
    }

    func testARealFailureKeepsTheTickAndRetriesAtTheNextSession() async {
        ConsentWireStub.grantResponse = { (500, Data("{}".utf8)) }
        await signUp()

        await signIn()
        XCTAssertEqual(ConsentWireStub.grantedTypes, [._0, ._1])

        ConsentWireStub.grantResponse = { (200, Data()) }
        await signIn()

        XCTAssertEqual(ConsentWireStub.grantedTypes, [._0, ._1, ._0, ._1])
    }

    func testAnotherAccountsSessionNeverReceivesThisAccountsTick() async {
        await signUp()

        await signIn(sessionEmail: "someone.else@example.com")
        XCTAssertEqual(ConsentWireStub.grantedTypes, [])

        await signIn()
        XCTAssertEqual(ConsentWireStub.grantedTypes, [._0, ._1])
    }

    func testAWithdrawnConsentIsNotResurrected() async {
        ConsentWireStub.consentsResponse = {
            (200, Data(#"[{"id":"c-1","consentType":0,"isGranted":false}]"#.utf8))
        }
        await signUp()

        await signIn()

        XCTAssertEqual(ConsentWireStub.grantedTypes, [._1])
    }

    func testAFailedConsentReadGrantsNothingAndKeepsTheTick() async {
        ConsentWireStub.consentsResponse = { (500, Data("{}".utf8)) }
        await signUp()

        await signIn()
        XCTAssertEqual(ConsentWireStub.grantedTypes, [])

        ConsentWireStub.consentsResponse = { (200, Data("[]".utf8)) }
        await signIn()
        XCTAssertEqual(ConsentWireStub.grantedTypes, [._0, ._1])
    }

    private func signUp() async {
        let viewModel = CustomerAuthViewModel(
            loginClient: spine,
            registrationClient: spine,
            emailConfirmationClient: spine,
            passwordResetClient: spine,
            socialAuthClient: spine,
            socialProvider: NoSocialProvider(),
            settings: UserDefaultsAppSettingsStore(defaults: defaults),
            snackbar: SnackbarController(),
            signupConsent: signupConsent
        )
        viewModel.onFirstNameChange("Ada")
        viewModel.onLastNameChange("Lovelace")
        viewModel.onSignUpEmailChange(email)
        viewModel.onSignUpPasswordChange("abcdefg1")
        viewModel.onConfirmPasswordChange("abcdefg1")
        viewModel.onAcceptTermsChange(true)
        await viewModel.signUp()
    }

    /// `sessionEmail` is what the SERVER answers with. The sign-in form always carries the
    /// registered address, so a helper that read the form instead of the token response would look
    /// correct here and still deliver one account's tick into another's session.
    private func signIn(sessionEmail: String? = nil) async {
        ConsentWireStub.sessionEmail = sessionEmail ?? email
        _ = await spine.login(email: email, password: "Passw0rd!")
    }
}

private struct FlowDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-flow"
    }
}

private struct NoRefreshClient: AuthRefreshing {
    func refresh(refreshToken _: String) async -> RefreshCallResult {
        .retryable
    }
}

private final class NoSocialProvider: SocialSignInProviding {
    func signInWithGoogle() async -> SocialSignInResult {
        .notConfigured
    }

    func signInWithApple() async -> SocialSignInResult {
        .notConfigured
    }
}

private final class FlowTokenStore: TokenStore, @unchecked Sendable {
    private let lock = NSLock()
    private var tokens: AuthTokens?

    func current() -> AuthTokens? {
        lock.withLock { tokens }
    }

    func save(_ tokens: AuthTokens) {
        lock.withLock { self.tokens = tokens }
    }

    func clear() {
        lock.withLock { tokens = nil }
    }
}
