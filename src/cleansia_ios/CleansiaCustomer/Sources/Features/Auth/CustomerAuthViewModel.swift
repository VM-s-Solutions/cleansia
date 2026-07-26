import CleansiaCore
import Combine
import Foundation

struct SignInFormState: Equatable {
    var email = ""
    var password = ""
    var emailError: String?
    var passwordError: String?
}

struct SignUpFormState: Equatable {
    var firstName = ""
    var lastName = ""
    var email = ""
    var password = ""
    var confirmPassword = ""
    var referralCode = ""
    var acceptTerms = false
    var firstNameError: String?
    var lastNameError: String?
    var emailError: String?
    var passwordError: String?
    var confirmPasswordError: String?
    var termsError: String?

    var passwordHasMinLength: Bool {
        PasswordPolicy.hasMinLength(password)
    }

    var passwordHasLetter: Bool {
        PasswordPolicy.hasLetter(password)
    }

    var passwordHasNumber: Bool {
        PasswordPolicy.hasNumber(password)
    }

    var passwordsMatch: Bool {
        PasswordPolicy.passwordsMatch(password, confirmPassword)
    }

    var isValid: Bool {
        !firstName.isBlank &&
            !lastName.isBlank &&
            !email.isBlank &&
            PasswordPolicy.isValid(password) &&
            passwordsMatch &&
            acceptTerms
    }
}

struct ForgotPasswordFormState: Equatable {
    var email = ""
    var emailError: String?
}

@MainActor
final class CustomerAuthViewModel: ViewModel {
    @Published private(set) var signInForm = SignInFormState()
    @Published private(set) var signUpForm = SignUpFormState()
    @Published private(set) var forgotForm = ForgotPasswordFormState()
    @Published private(set) var verifyCode = ""

    /// True once the reset code has actually been emailed, which is what moves the
    /// forgot-password screen from "type your address" to "type the code and a new password".
    @Published private(set) var resetCodeSent = false

    @Published private(set) var signInState: ActionState = .idle
    @Published private(set) var signUpState: ActionState = .idle
    @Published private(set) var forgotState: ActionState = .idle
    @Published private(set) var resetState: ActionState = .idle
    @Published private(set) var confirmState: ActionState = .idle
    @Published private(set) var resendState: ActionState = .idle
    @Published private(set) var socialState: ActionState = .idle

    /// Referral validation at signup, mirroring the booking sheet's state machine.
    /// Purely advisory: `Register` accepts a bad code fail-soft, so nothing here
    /// gates `signUpForm.isValid`.
    @Published private(set) var referralState: ReferralCodeState = .idle

    let outcome = PassthroughSubject<AuthOutcome, Never>()

    private let loginClient: LoginClient
    private let registrationClient: RegistrationAuthClient
    private let emailConfirmationClient: EmailConfirmationClient
    private let passwordResetClient: PasswordResetClient
    /// Second half of the reset: `Auth/ForgotPassword` mails the code, `User/ChangePassword`
    /// redeems it. The second endpoint is anonymous and customer-only, which is why this
    /// dependency is the customer-target `ChangePasswordClient` rather than another method on
    /// the shared `PasswordResetClient` — the partner mobile host has no such endpoint.
    private let changePasswordClient: ChangePasswordClient
    private let socialAuthClient: SocialAuthClient
    /// `Referral/Validate` is `[AllowAnonymous]` and sits on the customer
    /// `AnonymousAllowList`, so this runs with no session and no bearer — which is
    /// exactly what a signup screen needs.
    private let referralClient: ReferralClient
    private let socialProvider: SocialSignInProviding
    private let settings: AppSettingsStore
    private let snackbar: SnackbarController
    private let pendingEmail: String?
    private let errorLocalizer = ApiErrorLocalizer()

    /// One session lifetime for every mobile surface: 30 days, always requested.
    ///
    /// This constant used to be `false` — the inherited default of a "remember me" checkbox
    /// this screen has never actually shown — which asked for the 24-hour refresh token while
    /// the iOS partner app and both Android apps asked for the 30-day one. Remember-me is a
    /// shared-computer web idiom; on a personal handset it only buys a forced re-login after a
    /// day of not opening the app, and the security it implies is already carried by
    /// single-use rotating refresh tokens, the Keychain, and per-device revocation.
    ///
    /// The field stays on the wire because `Auth/Login` still declares it (and the web keeps
    /// its checkbox), so this is a client-side decision only — no server change.
    private static let rememberMe = true

    init(
        loginClient: LoginClient,
        registrationClient: RegistrationAuthClient,
        emailConfirmationClient: EmailConfirmationClient,
        passwordResetClient: PasswordResetClient,
        socialAuthClient: SocialAuthClient,
        socialProvider: SocialSignInProviding,
        settings: AppSettingsStore,
        snackbar: SnackbarController,
        pendingEmail: String? = nil,
        changePasswordClient: ChangePasswordClient = LiveChangePasswordClient(),
        referralClient: ReferralClient = LiveReferralClient()
    ) {
        self.loginClient = loginClient
        self.registrationClient = registrationClient
        self.emailConfirmationClient = emailConfirmationClient
        self.passwordResetClient = passwordResetClient
        self.changePasswordClient = changePasswordClient
        self.socialAuthClient = socialAuthClient
        self.referralClient = referralClient
        self.socialProvider = socialProvider
        self.settings = settings
        self.snackbar = snackbar
        self.pendingEmail = pendingEmail
    }

    var canResend: Bool {
        guard let pendingEmail else { return false }
        return !pendingEmail.isBlank
    }

    func onSignInEmailChange(_ value: String) {
        signInForm.email = value
        signInForm.emailError = nil
    }

    func onSignInPasswordChange(_ value: String) {
        signInForm.password = value
        signInForm.passwordError = nil
    }

    func signIn() async {
        if signInState.isSubmitting { return }
        guard validateSignIn() else { return }

        signInState = .submitting
        let result = await loginClient.login(
            email: signInForm.email,
            password: signInForm.password,
            rememberMe: Self.rememberMe
        )
        signInState = .idle
        emit(result, fallbackEmail: signInForm.email)
    }

    func onFirstNameChange(_ value: String) {
        signUpForm.firstName = value
        signUpForm.firstNameError = nil
    }

    func onLastNameChange(_ value: String) {
        signUpForm.lastName = value
        signUpForm.lastNameError = nil
    }

    func onSignUpEmailChange(_ value: String) {
        signUpForm.email = value
        signUpForm.emailError = nil
    }

    func onSignUpPasswordChange(_ value: String) {
        signUpForm.password = value
        signUpForm.passwordError = nil
    }

    func onConfirmPasswordChange(_ value: String) {
        signUpForm.confirmPassword = value
        signUpForm.confirmPasswordError = nil
    }

    func onReferralCodeChange(_ value: String) {
        signUpForm.referralCode = value
    }

    /// Checks the code against the server before the customer commits to it, so a
    /// typo surfaces here instead of silently costing them the referral bonus.
    /// Structurally identical to `BookingViewModel.validateReferralCode` — same
    /// client, same state machine, same normalisation — because it is the same
    /// endpoint answering the same question at a different point in the funnel.
    ///
    /// Only a code the server accepted is written to the form. A rejected or
    /// unreachable code leaves `referralCode` alone, so it never reaches `Register`
    /// on the strength of a failed check.
    @discardableResult
    func validateReferralCode(_ rawCode: String) async -> ReferralCodeState {
        let normalized = rawCode.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        if normalized.isEmpty {
            referralState = .idle
            return .idle
        }
        referralState = .validating
        let resolved: ReferralCodeState = switch await referralClient.validate(code: normalized) {
        case let .success(validation):
            if validation.isValid {
                .valid(referrerFirstName: validation.referrerFirstName)
            } else {
                .invalid(ReferralValidationError.from(validation.errorCode))
            }
        case .failure:
            .invalid(nil)
        }
        referralState = resolved
        if case .valid = resolved {
            signUpForm.referralCode = normalized
        }
        return resolved
    }

    /// Clearing the row must drop the payload too — a code the customer can no
    /// longer see must never reach the register call.
    func clearReferralCode() {
        referralState = .idle
        signUpForm.referralCode = ""
    }

    func onAcceptTermsChange(_ value: Bool) {
        signUpForm.acceptTerms = value
        signUpForm.termsError = nil
    }

    func signUp() async {
        if signUpState.isSubmitting { return }
        guard validateSignUp() else { return }

        let referralCode = signUpForm.referralCode.trimmingCharacters(in: .whitespacesAndNewlines)
        signUpState = .submitting
        let result = await registrationClient.register(
            email: signUpForm.email,
            password: signUpForm.password,
            firstName: signUpForm.firstName,
            lastName: signUpForm.lastName,
            language: settings.languageTag,
            referralCode: referralCode.isEmpty ? nil : referralCode
        )
        signUpState = .idle

        switch result {
        case .success:
            outcome.send(.needsEmailConfirm(email: signUpForm.email))
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }

    func onVerifyCodeChange(_ value: String) {
        verifyCode = String(value.filter(\.isNumber).prefix(confirmCodeLength))
    }

    func confirmEmail() async {
        if confirmState.isSubmitting { return }
        guard verifyCode.count == confirmCodeLength else { return }
        // The server verifies the 6-digit code against the email-named account, so email is required.
        guard let pendingEmail, !pendingEmail.isBlank else {
            snackbar.showError(L10n.Auth.errorGeneric)
            return
        }

        confirmState = .submitting
        let result = await emailConfirmationClient.confirmEmail(email: pendingEmail, code: verifyCode)
        confirmState = .idle

        switch result {
        case let .success(loginOutcome):
            switch loginOutcome {
            case .authenticated:
                outcome.send(.signedIn)
            case .unverifiedEmail:
                snackbar.showError(L10n.Auth.errorGeneric)
            }
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }

    func resendCode() async {
        if resendState.isSubmitting { return }
        guard let pendingEmail, !pendingEmail.isBlank else {
            snackbar.showError(L10n.Auth.errorEmailSendFailed)
            return
        }

        resendState = .submitting
        let result = await emailConfirmationClient.resendConfirmation(
            email: pendingEmail,
            language: settings.languageTag
        )
        resendState = .idle

        switch result {
        case .success:
            snackbar.showSuccess(L10n.Auth.resendSuccess)
        case .failure:
            snackbar.showError(L10n.Auth.errorEmailSendFailed)
        }
    }

    func onForgotEmailChange(_ value: String) {
        forgotForm.email = value
        forgotForm.emailError = nil
    }

    func requestPasswordReset() async {
        if forgotState.isSubmitting { return }
        guard validateForgot() else { return }

        forgotState = .submitting
        let result = await passwordResetClient.forgotPassword(
            email: forgotForm.email,
            language: settings.languageTag
        )
        forgotState = .idle

        switch result {
        case .success:
            snackbar.showSuccess(L10n.Auth.forgotCodeSent)
            // Do NOT emit .passwordReset here. The router maps it to .login, and emitting it at
            // "code sent" is the whole defect: it returned the customer to a sign-in screen they
            // still could not pass. The outcome belongs at the end of completePasswordReset.
            resetCodeSent = true
        case .failure:
            snackbar.showError(L10n.Auth.forgotSendFailed)
        }
    }

    /// Redeems the emailed code and sets the new password — the half of the reset the
    /// customer app never had. Structurally identical to `SecurityViewModel.changePassword`
    /// because it is the same endpoint; the only difference is that here the email comes from
    /// the forgot form rather than from a signed-in session.
    func completePasswordReset(code: String, newPassword: String, confirmPassword: String) async {
        guard !resetState.isSubmitting else { return }
        guard PasswordPolicy.isValid(newPassword) else {
            resetState = .error(L10n.Security.passwordPolicyError)
            return
        }
        guard PasswordPolicy.passwordsMatch(newPassword, confirmPassword) else {
            resetState = .error(L10n.Security.passwordMismatchError)
            return
        }

        resetState = .submitting
        let result = await changePasswordClient.changePassword(
            email: forgotForm.email,
            code: code.trimmingCharacters(in: .whitespacesAndNewlines),
            newPassword: newPassword
        )

        switch result {
        case .success:
            resetState = .idle
            snackbar.showSuccess(L10n.Security.changeSuccessSnackbar)
            outcome.send(.passwordReset)
        case let .failure(error):
            // ChangePassword charges a per-code attempt budget before it compares the hash, so a
            // few wrong guesses stop being "wrong code" and start being TooManyAttempts. Surface
            // whatever the server actually said rather than a generic retry prompt.
            let message = errorLocalizer.message(for: error)
            snackbar.showError(message)
            resetState = .error(message)
        }
    }

    func signInWithGoogle() async {
        if socialState.isSubmitting { return }
        socialState = .submitting
        let result = await socialProvider.signInWithGoogle()
        await handleSocial(result)
    }

    func signInWithApple() async {
        if socialState.isSubmitting { return }
        socialState = .submitting
        let result = await socialProvider.signInWithApple()
        await handleSocial(result)
    }

    private func handleSocial(_ result: SocialSignInResult) async {
        switch result {
        case let .google(credential):
            let auth = await socialAuthClient.googleAuth(
                token: credential.idToken,
                googleId: credential.googleId,
                email: credential.email,
                firstName: credential.firstName,
                lastName: credential.lastName
            )
            socialState = .idle
            emit(auth, fallbackEmail: credential.email)
        case let .apple(credential):
            let auth = await socialAuthClient.appleAuth(
                identityToken: credential.identityToken,
                rawNonce: credential.rawNonce,
                firstName: credential.firstName,
                lastName: credential.lastName
            )
            socialState = .idle
            emit(auth, fallbackEmail: "")
        case .cancelled:
            socialState = .idle
        case .noAccount:
            socialState = .idle
            snackbar.showWarning(L10n.Auth.socialNoAccount)
        case .notConfigured:
            socialState = .idle
            snackbar.showError(L10n.Auth.socialNotConfigured)
        case .failure:
            socialState = .idle
            snackbar.showError(L10n.Auth.socialFailed)
        }
    }

    private func emit(_ result: ApiResult<LoginOutcome>, fallbackEmail: String) {
        switch result {
        case let .success(loginOutcome):
            switch loginOutcome {
            case .authenticated:
                outcome.send(.signedIn)
            case let .unverifiedEmail(email, _):
                outcome.send(.needsEmailConfirm(email: email.isBlank ? fallbackEmail : email))
            }
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }

    private func validateSignIn() -> Bool {
        var valid = true
        if signInForm.email.isBlank {
            signInForm.emailError = L10n.Auth.errorEmailRequired
            valid = false
        } else if !EmailValidator.isValid(signInForm.email) {
            signInForm.emailError = L10n.Auth.errorEmailInvalid
            valid = false
        }
        if signInForm.password.isBlank {
            signInForm.passwordError = L10n.Auth.errorPasswordRequired
            valid = false
        }
        return valid
    }

    private func validateSignUp() -> Bool {
        var valid = true
        if signUpForm.firstName.isBlank {
            signUpForm.firstNameError = L10n.Auth.errorFirstNameRequired
            valid = false
        }
        if signUpForm.lastName.isBlank {
            signUpForm.lastNameError = L10n.Auth.errorLastNameRequired
            valid = false
        }
        if signUpForm.email.isBlank {
            signUpForm.emailError = L10n.Auth.errorEmailRequired
            valid = false
        } else if !EmailValidator.isValid(signUpForm.email) {
            signUpForm.emailError = L10n.Auth.errorEmailInvalid
            valid = false
        }
        if !PasswordPolicy.isValid(signUpForm.password) {
            signUpForm.passwordError = L10n.Auth.errorPasswordRules
            valid = false
        }
        if !signUpForm.passwordsMatch {
            signUpForm.confirmPasswordError = L10n.Auth.errorPasswordsNoMatch
            valid = false
        }
        if !signUpForm.acceptTerms {
            signUpForm.termsError = L10n.Auth.errorTermsRequired
            valid = false
        }
        return valid
    }

    private func validateForgot() -> Bool {
        if forgotForm.email.isBlank {
            forgotForm.emailError = L10n.Auth.errorEmailRequired
            return false
        }
        if !EmailValidator.isValid(forgotForm.email) {
            forgotForm.emailError = L10n.Auth.errorEmailInvalid
            return false
        }
        return true
    }
}

let confirmCodeLength = 6

#if DEBUG
    extension CustomerAuthViewModel {
        func forceSignInSubmittingForTest() {
            signInState = .submitting
        }

        func forceSignUpSubmittingForTest() {
            signUpState = .submitting
        }

        func forceSocialSubmittingForTest() {
            socialState = .submitting
        }

        func setVerifyCodeForTest(_ value: String) {
            verifyCode = value
        }
    }
#endif
