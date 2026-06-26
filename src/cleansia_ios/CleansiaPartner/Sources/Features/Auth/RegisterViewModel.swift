import CleansiaCore
import Combine
import Foundation

struct RegisterFormState: Equatable {
    var firstName = ""
    var lastName = ""
    var email = ""
    var password = ""
    var confirmPassword = ""
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

@MainActor
final class RegisterViewModel: ViewModel {
    @Published private(set) var form = RegisterFormState()
    @Published private(set) var registerState: ActionState = .idle

    let registerSuccess = PassthroughSubject<Void, Never>()

    private let client: RegistrationAuthClient
    private let settings: AppSettingsStore
    private let snackbar: SnackbarController

    init(client: RegistrationAuthClient, settings: AppSettingsStore, snackbar: SnackbarController) {
        self.client = client
        self.settings = settings
        self.snackbar = snackbar
    }

    func onFirstNameChange(_ value: String) {
        form.firstName = value
        form.firstNameError = nil
    }

    func onLastNameChange(_ value: String) {
        form.lastName = value
        form.lastNameError = nil
    }

    func onEmailChange(_ value: String) {
        form.email = value
        form.emailError = nil
    }

    func onPasswordChange(_ value: String) {
        form.password = value
        form.passwordError = nil
    }

    func onConfirmPasswordChange(_ value: String) {
        form.confirmPassword = value
        form.confirmPasswordError = nil
    }

    func onAcceptTermsChange(_ value: Bool) {
        form.acceptTerms = value
        form.termsError = nil
    }

    func register() async {
        if registerState.isSubmitting { return }
        guard validate() else { return }

        registerState = .submitting
        let result = await client.register(
            email: form.email,
            password: form.password,
            firstName: form.firstName,
            lastName: form.lastName,
            language: settings.languageTag
        )
        registerState = .idle

        switch result {
        case .success:
            registerSuccess.send(())
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }

    private func validate() -> Bool {
        var valid = true
        if form.firstName.isBlank {
            form.firstNameError = L10n.Register.errorFirstNameRequired
            valid = false
        }
        if form.lastName.isBlank {
            form.lastNameError = L10n.Register.errorLastNameRequired
            valid = false
        }
        if form.email.isBlank {
            form.emailError = L10n.Register.errorEmailRequired
            valid = false
        } else if !EmailValidator.isValid(form.email) {
            form.emailError = L10n.Register.errorEmailInvalid
            valid = false
        }
        if !PasswordPolicy.isValid(form.password) {
            form.passwordError = L10n.Register.errorPasswordRules
            valid = false
        }
        if !form.passwordsMatch {
            form.confirmPasswordError = L10n.Register.errorPasswordsNoMatch
            valid = false
        }
        if !form.acceptTerms {
            form.termsError = L10n.Register.errorTermsRequired
            valid = false
        }
        return valid
    }
}

#if DEBUG
    extension RegisterViewModel {
        func forceSubmittingForTest() {
            registerState = .submitting
        }
    }
#endif
