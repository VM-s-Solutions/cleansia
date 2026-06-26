import CleansiaCore
import Combine
import Foundation

struct LoginFormState: Equatable {
    var email = ""
    var password = ""
    var rememberMe = true
    var emailError: String?
    var passwordError: String?
}

struct LoginSuccess {
    let requiresEmailConfirmation: Bool
}

@MainActor
final class LoginViewModel: ViewModel {
    @Published private(set) var form = LoginFormState()
    @Published private(set) var loginState: ActionState = .idle

    let loginSuccess = PassthroughSubject<LoginSuccess, Never>()

    private let loginClient: LoginClient
    private let snackbar: SnackbarController

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

    func onRememberMeChange(_ rememberMe: Bool) {
        form.rememberMe = rememberMe
    }

    func login() async {
        if loginState.isSubmitting { return }
        guard validate() else { return }

        loginState = .submitting
        let result = await loginClient.login(
            email: form.email,
            password: form.password,
            rememberMe: form.rememberMe
        )
        loginState = .idle

        switch result {
        case let .success(outcome):
            loginSuccess.send(LoginSuccess(requiresEmailConfirmation: isUnverified(outcome)))
        case let .failure(error):
            snackbar.showApiError(error)
        }
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

    private func isUnverified(_ outcome: LoginOutcome) -> Bool {
        if case .unverifiedEmail = outcome { return true }
        return false
    }
}

#if DEBUG
    extension LoginViewModel {
        func forceSubmittingForTest() {
            loginState = .submitting
        }
    }
#endif
