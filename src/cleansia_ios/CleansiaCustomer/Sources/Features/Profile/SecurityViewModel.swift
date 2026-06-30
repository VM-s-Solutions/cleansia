import CleansiaCore
import Combine
import Foundation

@MainActor
final class SecurityViewModel: ViewModel {
    @Published private(set) var requestState: ActionState = .idle
    @Published private(set) var changeState: ActionState = .idle
    @Published var codeRequested = false

    let passwordChanged = PassthroughSubject<Void, Never>()

    let email: String
    private let language: String
    private let client: ChangePasswordClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    init(email: String, language: String, client: ChangePasswordClient, snackbar: SnackbarController) {
        self.email = email
        self.language = language
        self.client = client
        self.snackbar = snackbar
    }

    func requestCode() async {
        guard !requestState.isSubmitting else { return }
        requestState = .submitting
        switch await client.requestCode(email: email, language: language) {
        case .success:
            requestState = .idle
            codeRequested = true
            snackbar.showSuccess(L10n.Security.codeSentSnackbar)
        case let .failure(error):
            let message = localizer.message(for: error)
            snackbar.showError(message)
            requestState = .error(message)
        }
    }

    func changePassword(code: String, newPassword: String, confirmPassword: String) async {
        guard !changeState.isSubmitting else { return }
        guard PasswordPolicy.isValid(newPassword) else {
            changeState = .error(L10n.Security.passwordPolicyError)
            return
        }
        guard PasswordPolicy.passwordsMatch(newPassword, confirmPassword) else {
            changeState = .error(L10n.Security.passwordMismatchError)
            return
        }
        changeState = .submitting
        switch await client.changePassword(
            email: email,
            code: code.trimmingCharacters(in: .whitespacesAndNewlines),
            newPassword: newPassword
        ) {
        case .success:
            changeState = .idle
            snackbar.showSuccess(L10n.Security.changeSuccessSnackbar)
            passwordChanged.send()
        case let .failure(error):
            let message = localizer.message(for: error)
            snackbar.showError(message)
            changeState = .error(message)
        }
    }
}
