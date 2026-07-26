import CleansiaCore
import Combine
import Foundation

struct LoginFormState: Equatable {
    var email = ""
    var password = ""
    var emailError: String?
    var passwordError: String?
}

struct LoginSuccess: Equatable {
    let requiresEmailConfirmation: Bool
    let email: String?
}

@MainActor
final class LoginViewModel: ViewModel {
    @Published private(set) var form = LoginFormState()
    @Published private(set) var loginState: ActionState = .idle

    let loginSuccess = PassthroughSubject<LoginSuccess, Never>()

    private let loginClient: LoginClient
    private let snackbar: SnackbarController

    /// One session lifetime for every mobile surface: 30 days, always requested. This screen
    /// has no remember-me toggle and neither does the Android partner app any more — unlike the
    /// customer command, `MobilePartnerLogin` genuinely honours the flag, so unticking it really
    /// did sign a cleaner out after a day away. The field stays on the wire because the command
    /// declares it and the web keeps its own checkbox; no server change.
    private static let rememberMe = true

    init(loginClient: LoginClient, snackbar: SnackbarController) {
        self.loginClient = loginClient
        self.snackbar = snackbar
    }

    func onEmailChange(_ email: String) {
        form.email = email
        form.emailError = nil
    }

    func onPasswordChange(_ password: String) {
        form.password = password
        form.passwordError = nil
    }

    func login() async {
        if loginState.isSubmitting { return }
        guard validate() else { return }

        loginState = .submitting
        let result = await loginClient.login(
            email: form.email,
            password: form.password,
            rememberMe: Self.rememberMe
        )
        loginState = .idle

        switch result {
        case let .success(outcome):
            loginSuccess.send(makeSuccess(outcome))
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }

    private func makeSuccess(_ outcome: LoginOutcome) -> LoginSuccess {
        if case let .unverifiedEmail(email, _) = outcome {
            return LoginSuccess(requiresEmailConfirmation: true, email: email)
        }
        return LoginSuccess(requiresEmailConfirmation: false, email: nil)
    }

    private func validate() -> Bool {
        var valid = true
        if form.email.isBlank {
            form.emailError = L10n.loginErrorEmailRequired
            valid = false
        } else if !EmailValidator.isValid(form.email) {
            form.emailError = L10n.loginErrorEmailInvalid
            valid = false
        }
        if form.password.isBlank {
            form.passwordError = L10n.loginErrorPasswordRequired
            valid = false
        }
        return valid
    }
}

#if DEBUG
    extension LoginViewModel {
        func forceSubmittingForTest() {
            loginState = .submitting
        }
    }
#endif
