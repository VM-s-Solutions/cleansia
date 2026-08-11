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
    private var changePassword: FakeChangePasswordClient!
    private var social: FakeSocialAuthClient!
    private var provider: FakeSocialSignInProvider!
    private var settings: FakeAppSettingsStore!
    private var snackbar: SnackbarController!
    private var referral: FakeReferralClient!
    private var signupConsent: RecordingSignupConsent!
    private var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        signupConsent = RecordingSignupConsent()
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
        cancellables = []
    }

    override func tearDown() {
        cancellables = nil
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

    private func makeViewModel(pendingEmail: String? = nil) -> CustomerAuthViewModel {
        CustomerAuthViewModel(
            loginClient: login,
            registrationClient: registration,
            emailConfirmationClient: confirmation,
            passwordResetClient: passwordReset,
            socialAuthClient: social,
            socialProvider: provider,
            settings: settings,
            snackbar: snackbar,
            signupConsent: signupConsent,
            pendingEmail: pendingEmail,
            changePasswordClient: changePassword,
            referralClient: referral
        )
    }

    private func collectOutcome(_ vm: CustomerAuthViewModel) -> () -> AuthOutcome? {
        var received: AuthOutcome?
        vm.outcome.sink { received = $0 }.store(in: &cancellables)
        return { received }
    }

    private func fillValidSignUp(_ vm: CustomerAuthViewModel) {
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

    func testSignUpEnforcesPasswordPolicy() async {
        let vm = makeViewModel()
        fillValidSignUp(vm)
        vm.onSignUpPasswordChange("short")
        vm.onConfirmPasswordChange("short")

        await vm.signUp()

        XCTAssertNotNil(vm.signUpForm.passwordError)
        XCTAssertEqual(registration.callCount, 0)
    }

    func testSignUpRequiresMatchingPasswords() async {
        let vm = makeViewModel()
        fillValidSignUp(vm)
        vm.onConfirmPasswordChange("abcdefg2")

        await vm.signUp()

        XCTAssertNotNil(vm.signUpForm.confirmPasswordError)
        XCTAssertEqual(registration.callCount, 0)
    }

    /// The terms box is a hard blocker, not a hint. It is the reason the "unticked box parks
    /// nothing" rule in `SignupConsentRepository` can never fire from this screen — and the
    /// reason that rule cannot be the only thing pinning it.
    func testSignUpWithoutConsentSetsTermsErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        fillValidSignUp(vm)
        vm.onAcceptTermsChange(false)

        await vm.signUp()

        XCTAssertNotNil(vm.signUpForm.termsError)
        XCTAssertEqual(registration.callCount, 0)
        XCTAssertEqual(signupConsent.parked.count, 0)
    }

    func testASuccessfulSignUpParksTheTickAgainstTheSubmittedAddress() async {
        let vm = makeViewModel()
        fillValidSignUp(vm)

        await vm.signUp()

        XCTAssertEqual(signupConsent.parked.map(\.email), ["jana@b.cz"])
        XCTAssertEqual(signupConsent.parked.map(\.accepted), [true])
    }

    func testARejectedSignUpParksNothing() async {
        registration.result = .failure(ApiError(code: "user.existing_email", httpStatus: 400))
        let vm = makeViewModel()
        fillValidSignUp(vm)

        await vm.signUp()

        XCTAssertEqual(signupConsent.parked.count, 0)
    }

    func testSignUpFormStaysInvalidUntilConsentIsAccepted() {
        let vm = makeViewModel()
        fillValidSignUp(vm)
        vm.onAcceptTermsChange(false)
        XCTAssertFalse(vm.signUpForm.isValid)

        vm.onAcceptTermsChange(true)
        XCTAssertTrue(vm.signUpForm.isValid)
    }

    func testAcceptingConsentClearsTheTermsError() async {
        let vm = makeViewModel()
        fillValidSignUp(vm)
        vm.onAcceptTermsChange(false)
        await vm.signUp()
        XCTAssertNotNil(vm.signUpForm.termsError)

        vm.onAcceptTermsChange(true)

        XCTAssertNil(vm.signUpForm.termsError)
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

    /// The bug this pins: sending the code used to emit `.passwordReset`, which the router maps
    /// to `.login` — bouncing the customer back to a sign-in screen they still cannot pass,
    /// with a code in their inbox and nowhere to type it. Requesting the code must move the
    /// screen to its second step, not leave it.
    func testForgotPasswordSuccessOpensTheCodeStepInsteadOfLeavingTheScreen() async {
        passwordReset.result = .success(())
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        vm.onForgotEmailChange("a@b.cz")

        await vm.requestPasswordReset()

        XCTAssertTrue(vm.resetCodeSent)
        XCTAssertNil(received(), "the reset is not finished until the new password is accepted")
        XCTAssertEqual(snackbar.current?.severity, .success)
    }

    func testForgotPasswordFailureKeepsTheEmailStep() async {
        passwordReset.result = .failure(ApiError(code: "network.unreachable"))
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        vm.onForgotEmailChange("a@b.cz")

        await vm.requestPasswordReset()

        XCTAssertFalse(vm.resetCodeSent)
        XCTAssertNil(received())
        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    func testForgotPasswordInvalidEmailDoesNotSubmit() async {
        let vm = makeViewModel()
        vm.onForgotEmailChange("not-an-email")

        await vm.requestPasswordReset()

        XCTAssertNotNil(vm.forgotForm.emailError)
        XCTAssertEqual(passwordReset.callCount, 0)
    }

    func testCompleteResetSendsTheCodeAndFormEmailThenFinishesTheFlow() async throws {
        passwordReset.result = .success(())
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        vm.onForgotEmailChange("a@b.cz")
        await vm.requestPasswordReset()

        await vm.completePasswordReset(code: " 123456 ", newPassword: "abcdefg1", confirmPassword: "abcdefg1")

        let call = try XCTUnwrap(changePassword.changeCalls.first)
        XCTAssertEqual(call.email, "a@b.cz")
        XCTAssertEqual(call.code, "123456", "the code is trimmed before it reaches the server")
        XCTAssertEqual(call.newPassword, "abcdefg1")
        XCTAssertEqual(received(), .passwordReset)
        XCTAssertEqual(try CustomerRootView.Route.afterAuth(XCTUnwrap(received())), .login)
        XCTAssertEqual(vm.resetState, .idle)
    }

    func testCompleteResetRejectsAMismatchedConfirmationWithoutCallingTheServer() async {
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        vm.onForgotEmailChange("a@b.cz")

        await vm.completePasswordReset(code: "123456", newPassword: "abcdefg1", confirmPassword: "abcdefg2")

        XCTAssertEqual(changePassword.changeCalls.count, 0)
        XCTAssertNil(received())
        XCTAssertNotNil(vm.resetState.errorMessage)
    }

    func testCompleteResetEnforcesTheSamePasswordPolicyAsSignUp() async {
        let vm = makeViewModel()
        vm.onForgotEmailChange("a@b.cz")

        await vm.completePasswordReset(code: "123456", newPassword: "short", confirmPassword: "short")

        XCTAssertEqual(changePassword.changeCalls.count, 0)
        XCTAssertNotNil(vm.resetState.errorMessage)
    }

    func testCompleteResetFailureSurfacesTheServerErrorAndDoesNotFinish() async {
        changePassword.changePasswordResult = .failure(ApiError(code: "user.too_many_attempts"))
        let vm = makeViewModel()
        let received = collectOutcome(vm)
        vm.onForgotEmailChange("a@b.cz")

        await vm.completePasswordReset(code: "000000", newPassword: "abcdefg1", confirmPassword: "abcdefg1")

        XCTAssertNil(received(), "a rejected code must leave the customer on the reset step")
        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertNotNil(vm.resetState.errorMessage)
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

    func testGoogleSuccessAuthenticatedEmitsSignedInViaTheSpine() async throws {
        provider.googleResult = .google(.init(
            idToken: "g-token", googleId: "g-1", email: "a@b.cz", firstName: "A", lastName: "B"
        ))
        social.googleResult = .success(.authenticated)
        let vm = makeViewModel()
        let received = collectOutcome(vm)

        await vm.signInWithGoogle()

        XCTAssertEqual(received(), .signedIn)
        XCTAssertEqual(social.lastGoogle?.token, "g-token")
        XCTAssertEqual(social.lastGoogle?.googleId, "g-1")
        let outcome = try XCTUnwrap(received())
        XCTAssertEqual(CustomerRootView.Route.afterAuth(outcome), .home)
        XCTAssertEqual(vm.socialState, .idle)
    }

    func testGoogleUnverifiedEmitsNeedsEmailConfirmAndRouterMapsToVerify() async throws {
        provider.googleResult = .google(.init(
            idToken: "g-token", googleId: "g-1", email: "a@b.cz", firstName: "A", lastName: "B"
        ))
        social.googleResult = .success(.unverifiedEmail(email: "a@b.cz", hasToken: false))
        let vm = makeViewModel()
        let received = collectOutcome(vm)

        await vm.signInWithGoogle()

        XCTAssertEqual(received(), .needsEmailConfirm(email: "a@b.cz"))
        let outcome = try XCTUnwrap(received())
        XCTAssertEqual(CustomerRootView.Route.afterAuth(outcome), .verifyEmail(email: "a@b.cz"))
    }

    func testAppleSuccessAuthenticatedEmitsSignedIn() async {
        provider.appleResult = .apple(.init(
            identityToken: "apple-token", rawNonce: "raw", firstName: "A", lastName: "B"
        ))
        social.appleResult = .success(.authenticated)
        let vm = makeViewModel()
        let received = collectOutcome(vm)

        await vm.signInWithApple()

        XCTAssertEqual(received(), .signedIn)
        XCTAssertEqual(social.lastApple?.identityToken, "apple-token")
        XCTAssertEqual(social.lastApple?.rawNonce, "raw")
    }

    func testSocialCancelledIsSilentNoOutcomeNoSnackbar() async {
        provider.googleResult = .cancelled
        let vm = makeViewModel()
        let received = collectOutcome(vm)

        await vm.signInWithGoogle()

        XCTAssertNil(received())
        XCTAssertNil(snackbar.current)
        XCTAssertEqual(social.googleCallCount, 0)
        XCTAssertEqual(vm.socialState, .idle)
    }

    func testSocialNotConfiguredShowsErrorAndDoesNotCallSpine() async {
        provider.googleResult = .notConfigured
        let vm = makeViewModel()
        let received = collectOutcome(vm)

        await vm.signInWithGoogle()

        XCTAssertNil(received())
        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertEqual(social.googleCallCount, 0)
    }

    func testSocialNoAccountShowsWarning() async {
        provider.googleResult = .noAccount
        let vm = makeViewModel()

        await vm.signInWithGoogle()

        XCTAssertEqual(snackbar.current?.severity, .warning)
    }

    func testSocialFailureShowsError() async {
        provider.googleResult = .failure
        let vm = makeViewModel()

        await vm.signInWithGoogle()

        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    func testSocialReentryGuardWhileSubmitting() async {
        provider.googleResult = .google(.init(
            idToken: "t", googleId: "g", email: "a@b.cz", firstName: "A", lastName: "B"
        ))
        let vm = makeViewModel()
        vm.forceSocialSubmittingForTest()

        await vm.signInWithGoogle()

        XCTAssertEqual(provider.googleCallCount, 0)
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

    /// The GDPR record the tick owes, parked against the address the provider named so the spine
    /// can deliver it from inside the very call that opens the session.
    func testASocialSignUpParksTheTickAgainstTheProviderAddress() async {
        provider.googleResult = .google(.init(
            idToken: "g-token", googleId: "g-1", email: "a@b.cz", firstName: "A", lastName: "B"
        ))
        let vm = makeViewModel()
        vm.onAcceptTermsChange(true)

        await vm.signUpWithGoogle()

        XCTAssertEqual(signupConsent.parked.filter(\.accepted).map(\.email), ["a@b.cz"])
    }

    /// Apple never hands the client an address, so the identity token's claim is the only key
    /// available before the session exists.
    func testAnAppleSignUpParksTheTickAgainstTheIdentityTokenAddress() async {
        provider.appleResult = .apple(.init(
            identityToken: unsignedJwt(email: "relay@privaterelay.appleid.com"),
            rawNonce: "raw",
            firstName: nil,
            lastName: nil
        ))
        let vm = makeViewModel()
        vm.onAcceptTermsChange(true)

        await vm.signUpWithApple()

        XCTAssertEqual(
            signupConsent.parked.filter(\.accepted).map(\.email),
            ["relay@privaterelay.appleid.com"]
        )
    }

    func testASocialSignInParksNoAcceptedTick() async {
        provider.googleResult = .google(.init(
            idToken: "g-token", googleId: "g-1", email: "a@b.cz", firstName: "A", lastName: "B"
        ))
        let vm = makeViewModel()
        vm.onAcceptTermsChange(true)

        await vm.signInWithGoogle()

        XCTAssertEqual(signupConsent.parked.filter(\.accepted).count, 0)
    }

    private func unsignedJwt(email: String) -> String {
        let payload = Data(#"{"email":"\#(email)"}"#.utf8)
            .base64EncodedString()
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "=", with: "")
        return "header.\(payload).signature"
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

private final class FakeLoginClient: LoginClient {
    var result: ApiResult<LoginOutcome> = .success(.authenticated)
    private(set) var callCount = 0
    private(set) var lastRememberMe: Bool?

    func login(email _: String, password _: String, rememberMe: Bool) async -> ApiResult<LoginOutcome> {
        callCount += 1
        lastRememberMe = rememberMe
        return result
    }
}

private final class FakeRegistrationClient: RegistrationAuthClient {
    var result: ApiResult<Bool> = .success(true)
    private(set) var callCount = 0
    private(set) var lastLanguage: String?
    private(set) var lastReferralCode: String?

    func register(_ request: RegisterRequest) async -> ApiResult<Bool> {
        callCount += 1
        lastLanguage = request.language
        lastReferralCode = request.referralCode
        return result
    }
}

private final class FakeEmailConfirmationClient: EmailConfirmationClient {
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

private final class FakePasswordResetClient: PasswordResetClient {
    var result: ApiResult<Void> = .success(())
    private(set) var callCount = 0

    func forgotPassword(email _: String, language _: String) async -> ApiResult<Void> {
        callCount += 1
        return result
    }
}

private final class FakeSocialAuthClient: SocialAuthClient {
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
private final class FakeSocialSignInProvider: SocialSignInProviding {
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

private final class FakeAppSettingsStore: AppSettingsStore {
    private(set) var answeredPrompts: Set<String> = []
    func hasAnsweredPrompt(_ prompt: String, userId: String) -> Bool {
        answeredPrompts.contains("\(prompt)/\(userId)")
    }

    func markPromptAnswered(_ prompt: String, userId: String) {
        answeredPrompts.insert("\(prompt)/\(userId)")
    }

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
