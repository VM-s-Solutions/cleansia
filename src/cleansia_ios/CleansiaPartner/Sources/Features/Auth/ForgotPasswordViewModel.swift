import CleansiaCore
import Combine
import Foundation

struct ForgotPasswordFormState: Equatable {
    var email = ""
    var emailError: String?
}

@MainActor
final class ForgotPasswordViewModel: ViewModel {
    @Published private(set) var form = ForgotPasswordFormState()
    @Published private(set) var requestState: ActionState = .idle

    let requestSuccess = PassthroughSubject<Void, Never>()

    private let client: PasswordResetClient
    private let settings: AppSettingsStore
    private let snackbar: SnackbarController

    init(client: PasswordResetClient, settings: AppSettingsStore, snackbar: SnackbarController) {
        self.client = client
        self.settings = settings
        self.snackbar = snackbar
    }

    func onEmailChange(_ value: String) {
        form.email = value
        form.emailError = nil
    }

    func submit() async {
        if requestState.isSubmitting { return }
        guard validate() else { return }

        requestState = .submitting
        let result = await client.forgotPassword(email: form.email, language: settings.languageTag)
        requestState = .idle

        switch result {
        case .success:
            requestSuccess.send(())
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }

    private func validate() -> Bool {
        if form.email.isBlank {
            form.emailError = L10n.ForgotPassword.errorEmailRequired
            return false
        }
        if !EmailValidator.isValid(form.email) {
            form.emailError = L10n.ForgotPassword.errorEmailInvalid
            return false
        }
        return true
    }
}

#if DEBUG
    extension ForgotPasswordViewModel {
        func forceSubmittingForTest() {
            requestState = .submitting
        }
    }
#endif
