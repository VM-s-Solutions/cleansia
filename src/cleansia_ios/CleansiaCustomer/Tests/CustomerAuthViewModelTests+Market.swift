import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
extension CustomerAuthViewModelTests {
    // MARK: - The chosen market

    /// The persisted market names the operating company the account is created with, so every
    /// anonymous call that provisions or looks something up carries its country.
    func testSignUpSendsThePersistedMarketsCountry() async {
        market.send(.resolved(selected: MarketFixtures.slovakia, markets: MarketFixtures.two))
        let vm = makeViewModel()
        fillValidSignUp(vm)

        await vm.signUp()

        XCTAssertEqual(registration.lastCountryId, "svk")
    }

    func testSignUpWithNoMarketDirectorySendsNoCountrySoTheServerPicksTheDefault() async {
        let vm = makeViewModel()
        fillValidSignUp(vm)

        await vm.signUp()

        XCTAssertEqual(registration.callCount, 1)
        XCTAssertNil(registration.lastCountryId)
    }

    func testAMarketChosenAfterTheScreenOpenedIsTheOneSent() async {
        market.send(.resolved(selected: MarketFixtures.czechia, markets: MarketFixtures.two))
        let vm = makeViewModel()
        fillValidSignUp(vm)
        market.send(.resolved(selected: MarketFixtures.slovakia, markets: MarketFixtures.two))

        await vm.signUp()

        XCTAssertEqual(registration.lastCountryId, "svk")
    }

    func testSignUpWithGoogleSendsThePersistedMarketsCountry() async {
        market.send(.resolved(selected: MarketFixtures.slovakia, markets: MarketFixtures.two))
        provider.googleResult = .google(.init(
            idToken: "g-token", googleId: "g-1", email: "a@b.cz", firstName: "A", lastName: "B"
        ))
        let vm = makeViewModel()
        vm.onAcceptTermsChange(true)

        await vm.signUpWithGoogle()

        XCTAssertEqual(social.lastGoogle?.countryId, "svk")
    }

    func testSignUpWithAppleSendsThePersistedMarketsCountry() async {
        market.send(.resolved(selected: MarketFixtures.slovakia, markets: MarketFixtures.two))
        provider.appleResult = .apple(.init(
            identityToken: "apple-token", rawNonce: "raw", firstName: nil, lastName: nil
        ))
        let vm = makeViewModel()
        vm.onAcceptTermsChange(true)

        await vm.signUpWithApple()

        XCTAssertEqual(social.lastApple?.countryId, "svk")
    }

    /// A sign-in that resolves an existing account ignores the market server-side, but a first
    /// sign-in provisions with it, and the client cannot tell the two apart before the call.
    func testSignInWithGoogleSendsThePersistedMarketsCountryToo() async {
        market.send(.resolved(selected: MarketFixtures.slovakia, markets: MarketFixtures.two))
        provider.googleResult = .google(.init(
            idToken: "g-token", googleId: "g-1", email: "a@b.cz", firstName: "A", lastName: "B"
        ))
        let vm = makeViewModel()

        await vm.signInWithGoogle()

        XCTAssertEqual(social.lastGoogle?.countryId, "svk")
    }

    func testReferralValidationSendsThePersistedMarketsCountry() async {
        market.send(.resolved(selected: MarketFixtures.slovakia, markets: MarketFixtures.two))
        let vm = makeViewModel()

        await vm.validateReferralCode("anna7")

        XCTAssertEqual(referral.lastCountryId, "svk")
    }

    func testReferralValidationWithNoMarketDirectorySendsNoCountry() async {
        let vm = makeViewModel()

        await vm.validateReferralCode("anna7")

        XCTAssertEqual(referral.callCount, 1)
        XCTAssertNil(referral.lastCountryId)
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

    /// The terms box is a hard blocker, not a hint: an unticked form never reaches the wire, so the
    /// server is never asked to record a consent nobody gave.
    func testSignUpWithoutConsentSetsTermsErrorAndDoesNotSubmit() async {
        let vm = makeViewModel()
        fillValidSignUp(vm)
        vm.onAcceptTermsChange(false)

        await vm.signUp()

        XCTAssertNotNil(vm.signUpForm.termsError)
        XCTAssertEqual(registration.callCount, 0)
        XCTAssertNil(registration.lastTermsAccepted)
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
}
