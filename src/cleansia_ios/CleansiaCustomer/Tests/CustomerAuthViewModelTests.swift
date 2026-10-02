import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
final class CustomerAuthViewModelTests: XCTestCase {
    var login: FakeLoginClient!
    var registration: FakeRegistrationClient!
    var confirmation: FakeEmailConfirmationClient!
    var passwordReset: FakePasswordResetClient!
    var changePassword: FakeChangePasswordClient!
    var social: FakeSocialAuthClient!
    var provider: FakeSocialSignInProvider!
    var settings: FakeAppSettingsStore!
    var snackbar: SnackbarController!
    var referral: FakeReferralClient!
    var market: CurrentValueSubject<MarketState, Never>!
    var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        login = FakeLoginClient()
        registration = FakeRegistrationClient()
        confirmation = FakeEmailConfirmationClient()
        passwordReset = FakePasswordResetClient()
        changePassword = FakeChangePasswordClient()
        social = FakeSocialAuthClient()
        provider = FakeSocialSignInProvider()
        settings = FakeAppSettingsStore()
        snackbar = SnackbarController()
        referral = FakeReferralClient()
        market = CurrentValueSubject(.unavailable)
        cancellables = []
    }

    override func tearDown() {
        cancellables = nil
        market = nil
        referral = nil
        snackbar = nil
        settings = nil
        provider = nil
        social = nil
        changePassword = nil
        passwordReset = nil
        confirmation = nil
        registration = nil
        login = nil
        super.tearDown()
    }

    func makeViewModel(pendingEmail: String? = nil) -> CustomerAuthViewModel {
        CustomerAuthViewModel(
            loginClient: login,
            registrationClient: registration,
            emailConfirmationClient: confirmation,
            passwordResetClient: passwordReset,
            socialAuthClient: social,
            socialProvider: provider,
            settings: settings,
            snackbar: snackbar,
            pendingEmail: pendingEmail,
            changePasswordClient: changePassword,
            referralClient: referral,
            market: market.eraseToAnyPublisher()
        )
    }

    func collectOutcome(_ vm: CustomerAuthViewModel) -> () -> AuthOutcome? {
        var received: AuthOutcome?
        vm.outcome.sink { received = $0 }.store(in: &cancellables)
        return { received }
    }

    func fillValidSignUp(_ vm: CustomerAuthViewModel) {
        vm.onFirstNameChange("Jana")
        vm.onLastNameChange("Nováková")
        vm.onSignUpEmailChange("jana@b.cz")
        vm.onSignUpPasswordChange("abcdefg1")
        vm.onConfirmPasswordChange("abcdefg1")
        vm.onAcceptTermsChange(true)
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

    /// One mobile-wide session lifetime. There is no remember-me toggle on this screen and
    /// there never was one to inherit an "unchecked" default from — the constant used to be
    /// `false`, which asked the server for the 24-hour refresh token. Every other mobile
    /// surface (iOS partner, both Android apps) asks for the 30-day one, so this is the
    /// odd-one-out being brought into line rather than a preference being expressed.
    func testSignInAlwaysRequestsTheLongLivedRefreshToken() async {
        let vm = makeViewModel()
        vm.onSignInEmailChange("a@b.cz")
        vm.onSignInPasswordChange("secret")

        await vm.signIn()

        XCTAssertEqual(login.lastRememberMe, true)
    }

    func testSignUpSuccessEmitsNeedsEmailConfirmCarryingFormEmail() async {
        registration.result = .success(true)
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        fillValidSignUp(vm)

        await vm.signUp()

        XCTAssertEqual(received(), .needsEmailConfirm(email: "jana@b.cz"))
        XCTAssertEqual(registration.callCount, 1)
    }

    /// The tick rides the registration itself: the server grants Terms of Service and Privacy Policy
    /// in the same commit that creates the account, so nothing is parked on the device any more.
    func testSignUpSendsTheTickOnTheRegistrationItself() async {
        registration.result = .success(true)
        let vm = makeViewModel()
        fillValidSignUp(vm)

        await vm.signUp()

        XCTAssertEqual(registration.lastTermsAccepted, true)
    }

    func testSignUpThreadsTrimmedReferralCodeToRegister() async {
        registration.result = .success(true)
        let vm = makeViewModel()
        fillValidSignUp(vm)
        vm.onReferralCodeChange("  ANNA7 ")

        await vm.signUp()

        XCTAssertEqual(registration.lastReferralCode, "ANNA7")
    }

    func testSignUpSendsNilReferralWhenBlank() async {
        registration.result = .success(true)
        let vm = makeViewModel()
        fillValidSignUp(vm)
        vm.onReferralCodeChange("   ")

        await vm.signUp()

        XCTAssertNil(registration.lastReferralCode)
    }

    // MARK: - Referral validation at signup

    func testValidatingAGoodReferralCodeAppliesTheNormalisedCode() async {
        referral.result = .success(ReferralValidation(isValid: true, referrerFirstName: "Eva", errorCode: nil))
        let vm = makeViewModel()

        let outcome = await vm.validateReferralCode(" anna7 ")

        XCTAssertEqual(outcome, .valid(referrerFirstName: "Eva"))
        XCTAssertEqual(vm.referralState, .valid(referrerFirstName: "Eva"))
        XCTAssertEqual(vm.signUpForm.referralCode, "ANNA7")
        XCTAssertEqual(referral.lastCode, "ANNA7")
    }

    func testAValidatedReferralCodeIsTheOneSentToRegister() async {
        referral.result = .success(ReferralValidation(isValid: true, referrerFirstName: "Eva", errorCode: nil))
        registration.result = .success(true)
        let vm = makeViewModel()
        fillValidSignUp(vm)
        _ = await vm.validateReferralCode("anna7")

        await vm.signUp()

        XCTAssertEqual(registration.lastReferralCode, "ANNA7")
    }

    func testRejectedReferralCodeMapsTheServerErrorAndIsNotApplied() async {
        referral.result = .success(ReferralValidation(
            isValid: false,
            referrerFirstName: nil,
            errorCode: "SelfReferral"
        ))
        let vm = makeViewModel()

        let outcome = await vm.validateReferralCode("MYOWN")

        XCTAssertEqual(outcome, .invalid(.selfReferral))
        XCTAssertEqual(vm.referralState, .invalid(.selfReferral))
        XCTAssertEqual(vm.signUpForm.referralCode, "")
    }

    func testReferralTransportFailureIsGenericInvalidNotAFatalError() async {
        referral.result = .failure(ApiError(code: "network"))
        let vm = makeViewModel()

        let outcome = await vm.validateReferralCode("ANNA7")

        XCTAssertEqual(outcome, .invalid(nil))
        XCTAssertEqual(vm.referralState, .invalid(nil))
        XCTAssertEqual(vm.signUpForm.referralCode, "")
    }

    func testBlankReferralCodeShortCircuitsWithoutCallingTheClient() async {
        let vm = makeViewModel()

        let outcome = await vm.validateReferralCode("   ")

        XCTAssertEqual(outcome, .idle)
        XCTAssertEqual(vm.referralState, .idle)
        XCTAssertEqual(referral.callCount, 0)
    }

    func testClearingTheReferralDropsBothTheStateAndThePayload() async {
        referral.result = .success(ReferralValidation(isValid: true, referrerFirstName: "Eva", errorCode: nil))
        let vm = makeViewModel()
        _ = await vm.validateReferralCode("ANNA7")

        vm.clearReferralCode()

        XCTAssertEqual(vm.referralState, .idle)
        XCTAssertEqual(vm.signUpForm.referralCode, "")
    }

    /// A rejected code must not block registration — `Register.cs` accepts a bad
    /// referral fail-soft, so the sign-up button stays live and the code still
    /// goes over the wire for the server to ignore.
    func testARejectedReferralCodeDoesNotBlockSignUp() async {
        referral.result = .success(ReferralValidation(isValid: false, referrerFirstName: nil, errorCode: "NotFound"))
        registration.result = .success(true)
        let vm = makeViewModel()
        fillValidSignUp(vm)
        vm.onReferralCodeChange("NOPE")
        _ = await vm.validateReferralCode("NOPE")

        await vm.signUp()

        XCTAssertTrue(vm.signUpForm.isValid)
        XCTAssertEqual(registration.callCount, 1)
    }

    // MARK: - The signup gate (Q-CONSENT-01) and the sign-in refusal (Q-CONSENT-02)

    /// The tick is what tells a signup apart from a sign-in on the wire; the two screens hit one
    /// endpoint and differ in nothing else. Asserted through the production path with the gate
    /// live and satisfied — never by disabling the gate first.
    func testSignUpWithGoogleAssertsTheTickOnTheRequest() async {
        provider.googleResult = .google(.init(
            idToken: "g-token", googleId: "g-1", email: "a@b.cz", firstName: "A", lastName: "B"
        ))
        let vm = makeViewModel()
        vm.onAcceptTermsChange(true)

        await vm.signUpWithGoogle()

        XCTAssertEqual(social.lastGoogle?.termsAccepted, true)
    }

    func testSignUpWithAppleAssertsTheTickOnTheRequest() async {
        provider.appleResult = .apple(.init(
            identityToken: "apple-token", rawNonce: "raw", firstName: nil, lastName: nil
        ))
        let vm = makeViewModel()
        vm.onAcceptTermsChange(true)

        await vm.signUpWithApple()

        XCTAssertEqual(social.lastApple?.termsAccepted, true)
    }

    /// An untick stops the flow at the tap: no provider sheet, no request, and no spinner left
    /// running. The refusal is spoken, because a control that does nothing explains nothing.
    func testSignUpWithGoogleWithoutTheTickNeverStartsTheFlow() async {
        provider.googleResult = .google(.init(
            idToken: "g-token", googleId: "g-1", email: "a@b.cz", firstName: "A", lastName: "B"
        ))
        let vm = makeViewModel()
        vm.onAcceptTermsChange(false)

        await vm.signUpWithGoogle()

        XCTAssertEqual(provider.googleCallCount, 0)
        XCTAssertEqual(social.googleCallCount, 0)
        XCTAssertEqual(vm.socialState, .idle)
        XCTAssertEqual(snackbar.current?.text, L10n.Auth.socialTermsRequired)
        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    func testSignUpWithAppleWithoutTheTickNeverStartsTheFlow() async {
        provider.appleResult = .apple(.init(
            identityToken: "apple-token", rawNonce: "raw", firstName: nil, lastName: nil
        ))
        let vm = makeViewModel()
        vm.onAcceptTermsChange(false)

        await vm.signUpWithApple()

        XCTAssertEqual(provider.appleCallCount, 0)
        XCTAssertEqual(social.appleCallCount, 0)
        XCTAssertEqual(vm.socialState, .idle)
        XCTAssertEqual(snackbar.current?.text, L10n.Auth.socialTermsRequired)
    }

    /// The sign-in screen has no terms box, so its buttons assert nothing — and the tick is a
    /// property of the METHOD, not of the object's state. One view model, both screens: a ticked
    /// signup form left behind on the same instance must not leak into a sign-in request.
    func testSignInWithGoogleAssertsNothingEvenWithTheSignUpBoxTicked() async {
        provider.googleResult = .google(.init(
            idToken: "g-token", googleId: "g-1", email: "a@b.cz", firstName: "A", lastName: "B"
        ))
        let vm = makeViewModel()
        vm.onAcceptTermsChange(true)

        await vm.signInWithGoogle()

        XCTAssertEqual(social.googleCallCount, 1)
        XCTAssertEqual(social.lastGoogle?.termsAccepted, false)
    }

    func testSignInWithAppleAssertsNothingEvenWithTheSignUpBoxTicked() async {
        provider.appleResult = .apple(.init(
            identityToken: "apple-token", rawNonce: "raw", firstName: nil, lastName: nil
        ))
        let vm = makeViewModel()
        vm.onAcceptTermsChange(true)

        await vm.signInWithApple()

        XCTAssertEqual(social.appleCallCount, 1)
        XCTAssertEqual(social.lastApple?.termsAccepted, false)
    }

    /// The refusal an unasserted call now earns. It must read as itself — "no account, sign up
    /// first" — and not collapse into the generic "couldn't sign you in" or the raw business key.
    func testTheSocialAccountNotFoundRefusalRendersItsOwnMessage() async {
        provider.googleResult = .google(.init(
            idToken: "g-token", googleId: "g-1", email: "a@b.cz", firstName: "A", lastName: "B"
        ))
        social.googleResult = .failure(ApiError(code: "auth.social_account_not_found", httpStatus: 400))
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        let localizer = ApiErrorLocalizer()

        await vm.signInWithGoogle()

        let shown = snackbar.current?.text
        XCTAssertEqual(shown, localizer.message(for: ApiError(code: "auth.social_account_not_found")))
        XCTAssertNotEqual(shown, "auth.social_account_not_found", "the catalog entry is missing")
        XCTAssertNotEqual(shown, localizer.message(forStatus: 400))
        XCTAssertNotEqual(shown, L10n.Auth.socialFailed)
        XCTAssertNil(received())
    }

    func testAppleNonceFlowRawToBackendHashedToApple() {
        let raw = Nonce.randomRaw()
        let other = Nonce.randomRaw()
        XCTAssertNotEqual(raw, other, "the raw nonce must be cryptographically random per request")
        XCTAssertEqual(raw.count, 32)

        let hashed = Nonce.sha256(raw)
        XCTAssertEqual(hashed.count, 64)
        XCTAssertNotEqual(hashed, raw, "the value sent to Apple is the SHA256, not the raw nonce")
        XCTAssertEqual(hashed, Nonce.sha256(raw), "hashing is deterministic")

        let knownDigest = Nonce.sha256("abc")
        XCTAssertEqual(knownDigest, "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")
    }

    func testAppleSpinePostsRawNonceNotHashed() async throws {
        let raw = Nonce.randomRaw()
        provider.appleResult = .apple(.init(
            identityToken: "apple-token", rawNonce: raw, firstName: nil, lastName: nil
        ))
        social.appleResult = .success(.authenticated)
        let vm = makeViewModel()

        await vm.signInWithApple()

        let posted = try XCTUnwrap(social.lastApple)
        XCTAssertEqual(posted.rawNonce, raw)
        XCTAssertNotEqual(posted.rawNonce, Nonce.sha256(raw))
    }
}

final class FakeLoginClient: LoginClient {
    var result: ApiResult<LoginOutcome> = .success(.authenticated)
    private(set) var callCount = 0
    private(set) var lastRememberMe: Bool?

    func login(email _: String, password _: String, rememberMe: Bool) async -> ApiResult<LoginOutcome> {
        callCount += 1
        lastRememberMe = rememberMe
        return result
    }
}

final class FakeRegistrationClient: RegistrationAuthClient {
    var result: ApiResult<Bool> = .success(true)
    private(set) var callCount = 0
    private(set) var lastLanguage: String?
    private(set) var lastReferralCode: String?
    private(set) var lastCountryId: String?
    private(set) var lastTermsAccepted: Bool?

    func register(_ request: RegisterRequest) async -> ApiResult<Bool> {
        callCount += 1
        lastLanguage = request.language
        lastReferralCode = request.referralCode
        lastCountryId = request.countryId
        lastTermsAccepted = request.termsAccepted
        return result
    }
}

final class FakeEmailConfirmationClient: EmailConfirmationClient {
    var confirmResult: ApiResult<LoginOutcome> = .success(.authenticated)
    var resendResult: ApiResult<Bool> = .success(true)
    private(set) var resendCallCount = 0
    private(set) var lastResendArgs: (email: String, language: String)?

    func confirmEmail(email _: String, code _: String) async -> ApiResult<LoginOutcome> {
        confirmResult
    }

    func resendConfirmation(email: String, language: String) async -> ApiResult<Bool> {
        resendCallCount += 1
        lastResendArgs = (email, language)
        return resendResult
    }
}

final class FakePasswordResetClient: PasswordResetClient {
    var result: ApiResult<Void> = .success(())
    private(set) var callCount = 0

    func forgotPassword(email _: String, language _: String) async -> ApiResult<Void> {
        callCount += 1
        return result
    }
}

final class FakeSocialAuthClient: SocialAuthClient {
    var googleResult: ApiResult<LoginOutcome> = .success(.authenticated)
    var appleResult: ApiResult<LoginOutcome> = .success(.authenticated)
    private(set) var googleCallCount = 0
    private(set) var appleCallCount = 0
    private(set) var lastGoogle: GoogleAuthRequest?
    private(set) var lastApple: AppleAuthRequest?

    func googleAuth(_ request: GoogleAuthRequest) async -> ApiResult<LoginOutcome> {
        googleCallCount += 1
        lastGoogle = request
        return googleResult
    }

    func appleAuth(_ request: AppleAuthRequest) async -> ApiResult<LoginOutcome> {
        appleCallCount += 1
        lastApple = request
        return appleResult
    }
}

@MainActor
final class FakeSocialSignInProvider: SocialSignInProviding {
    var googleResult: SocialSignInResult = .cancelled
    var appleResult: SocialSignInResult = .cancelled
    private(set) var googleCallCount = 0
    private(set) var appleCallCount = 0

    func signInWithGoogle() async -> SocialSignInResult {
        googleCallCount += 1
        return googleResult
    }

    func signInWithApple() async -> SocialSignInResult {
        appleCallCount += 1
        return appleResult
    }
}
