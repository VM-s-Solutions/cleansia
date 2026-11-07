import CleansiaCore
import Combine
import Foundation

private let confirmCodeLength = 6

@MainActor
final class ConfirmEmailViewModel: ViewModel {
    @Published private(set) var code = ""
    @Published private(set) var passwordForm = ChosenPassword()
    @Published private(set) var confirmState: ActionState = .idle
    @Published private(set) var resendState: ActionState = .idle

    let confirmSuccess = PassthroughSubject<Void, Never>()

    private let email: String?
    private let registeredPassword: String?
    private let client: EmailConfirmationClient
    private let settings: AppSettingsStore
    private let snackbar: SnackbarController

    /// `registeredPassword` is the one typed on the registration that just sent the code, or nil when the
    /// address is confirmed later. The confirmation sets the password it carries.
    init(
        email: String?,
        registeredPassword: String?,
        client: EmailConfirmationClient,
        settings: AppSettingsStore,
        snackbar: SnackbarController
    ) {
        self.email = email
        self.registeredPassword = registeredPassword
        self.client = client
        self.settings = settings
        self.snackbar = snackbar
    }

    var canResend: Bool {
        guard let email else { return false }
        return !email.isBlank
    }

    var asksForPassword: Bool {
        registeredPassword == nil
    }

    /// The code's sixth digit confirms on its own only once there is a password to send; until then Verify does.
    var isReadyToConfirm: Bool {
        code.count == confirmCodeLength && (!asksForPassword || passwordForm.isValid)
    }

    func onCodeChange(_ value: String) {
        code = String(value.filter(\.isNumber).prefix(confirmCodeLength))
    }

    func onPasswordChange(_ value: String) {
        passwordForm.setPassword(value)
    }

    func onConfirmPasswordChange(_ value: String) {
        passwordForm.setConfirmation(value)
    }

    func confirmEmail() async {
        if confirmState.isSubmitting { return }
        guard code.count == confirmCodeLength else { return }
        // The 6-digit code only proves possession relative to the account it was issued to — the
        // server verifies it against the email-named account, so without the email nothing can match.
        guard let email, !email.isBlank else {
            snackbar.showError(L10n.ConfirmEmail.errorGeneric)
            return
        }
        guard let password = registeredPassword ?? passwordForm.validated(
            rulesError: L10n.Register.errorPasswordRules,
            mismatchError: L10n.Register.errorPasswordsNoMatch
        ) else { return }

        confirmState = .submitting
        let result = await client.confirmEmail(email: email, code: code, password: password)
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
