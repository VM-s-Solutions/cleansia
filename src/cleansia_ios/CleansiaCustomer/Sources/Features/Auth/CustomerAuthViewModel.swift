import CleansiaCore
import Combine
import Foundation

struct SignInFormState: Equatable {
    var email = ""
    var password = ""
    var rememberMe = false
    var emailError: String?
    var passwordError: String?
}

struct SignUpFormState: Equatable {
    var firstName = ""
    var lastName = ""
    var email = ""
    var password = ""
    var confirmPassword = ""
    var firstNameError: String?
    var lastNameError: String?
    var emailError: String?
    var passwordError: String?
    var confirmPasswordError: String?

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
            passwordsMatch
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

    @Published private(set) var signInState: ActionState = .idle
    @Published private(set) var signUpState: ActionState = .idle
    @Published private(set) var forgotState: ActionState = .idle
    @Published private(set) var confirmState: ActionState = .idle
    @Published private(set) var resendState: ActionState = .idle
    @Published private(set) var socialState: ActionState = .idle

    let outcome = PassthroughSubject<AuthOutcome, Never>()

    private let loginClient: LoginClient
    private let registrationClient: RegistrationAuthClient
    private let emailConfirmationClient: EmailConfirmationClient
    private let passwordResetClient: PasswordResetClient
    private let socialAuthClient: SocialAuthClient
    private let socialProvider: SocialSignInProviding
    private let settings: AppSettingsStore
    private let snackbar: SnackbarController
    private let pendingEmail: String?

    init(
        loginClient: LoginClient,
        registrationClient: RegistrationAuthClient,
        emailConfirmationClient: EmailConfirmationClient,
        passwordResetClient: PasswordResetClient,
        socialAuthClient: SocialAuthClient,
        socialProvider: SocialSignInProviding,
        settings: AppSettingsStore,
        snackbar: SnackbarController,
        pendingEmail: String? = nil
    ) {
        self.loginClient = loginClient
        self.registrationClient = registrationClient
        self.emailConfirmationClient = emailConfirmationClient
        self.passwordResetClient = passwordResetClient
        self.socialAuthClient = socialAuthClient
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

    func onRememberMeChange(_ value: Bool) {
        signInForm.rememberMe = value
    }

    func signIn() async {
        if signInState.isSubmitting { return }
        guard validateSignIn() else { return }

        signInState = .submitting
        let result = await loginClient.login(
            email: signInForm.email,
            password: signInForm.password,
            rememberMe: signInForm.rememberMe
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

    func signUp() async {
        if signUpState.isSubmitting { return }
        guard validateSignUp() else { return }

        signUpState = .submitting
        let result = await registrationClient.register(
            email: signUpForm.email,
            password: signUpForm.password,
            firstName: signUpForm.firstName,
            lastName: signUpForm.lastName,
            language: settings.languageTag
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
        // The 6-digit code only proves possession relative to the account it was issued to — the
        // server verifies it against the email-named account, so without the email nothing can match.
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
            outcome.send(.passwordReset)
        case .failure:
            snackbar.showError(L10n.Auth.forgotSendFailed)
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
