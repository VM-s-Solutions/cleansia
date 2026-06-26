import CleansiaCore
import Combine
import Foundation

private let confirmCodeLength = 6

@MainActor
final class ConfirmEmailViewModel: ViewModel {
    @Published private(set) var code = ""
    @Published private(set) var confirmState: ActionState = .idle
    @Published private(set) var resendState: ActionState = .idle

    let confirmSuccess = PassthroughSubject<Void, Never>()

    private let email: String?
    private let client: EmailConfirmationClient
    private let settings: AppSettingsStore
    private let snackbar: SnackbarController

    init(
        email: String?,
        client: EmailConfirmationClient,
        settings: AppSettingsStore,
        snackbar: SnackbarController
    ) {
        self.email = email
        self.client = client
        self.settings = settings
        self.snackbar = snackbar
    }

    var canResend: Bool {
        guard let email else { return false }
        return !email.isBlank
    }

    func onCodeChange(_ value: String) {
        code = String(value.filter(\.isNumber).prefix(confirmCodeLength))
    }

    func confirmEmail() async {
        if confirmState.isSubmitting { return }
        guard code.count == confirmCodeLength else { return }

        confirmState = .submitting
        let result = await client.confirmEmail(code: code)
        confirmState = .idle

        switch result {
        case let .success(outcome):
            switch outcome {
            case .authenticated:
                confirmSuccess.send(())
            case .unverifiedEmail:
                snackbar.showError(L10n.ConfirmEmail.errorGeneric)
            }
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }

    func resendCode() async {
        if resendState.isSubmitting { return }
        guard let email, !email.isBlank else {
            snackbar.showError(L10n.ConfirmEmail.errorGeneric)
            return
        }

        resendState = .submitting
        let result = await client.resendConfirmation(email: email, language: settings.languageTag)
        resendState = .idle

        switch result {
        case .success:
            snackbar.showSuccess(L10n.ConfirmEmail.resendSent)
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }
}

#if DEBUG
    extension ConfirmEmailViewModel {
        func setCodeForTest(_ value: String) {
            code = value
        }
    }
#endif
