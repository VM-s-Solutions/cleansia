import CleansiaCore
import SwiftUI

struct RegisterView: View {
    @StateObject private var vm: RegisterViewModel
    let onSignIn: () -> Void
    let onRegistered: () -> Void

    init(
        client: RegistrationAuthClient,
        settings: AppSettingsStore,
        snackbar: SnackbarController,
        onSignIn: @escaping () -> Void,
        onRegistered: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: RegisterViewModel(
            client: client,
            settings: settings,
            snackbar: snackbar
        ))
        self.onSignIn = onSignIn
        self.onRegistered = onRegistered
    }

    var body: some View {
        RegisterContent(
            form: vm.form,
            isLoading: vm.registerState.isSubmitting,
            onFirstNameChange: vm.onFirstNameChange,
            onLastNameChange: vm.onLastNameChange,
            onEmailChange: vm.onEmailChange,
            onPasswordChange: vm.onPasswordChange,
            onConfirmPasswordChange: vm.onConfirmPasswordChange,
            onAcceptTermsChange: vm.onAcceptTermsChange,
            onSignIn: onSignIn,
            onSubmit: { Task { await vm.register() } }
        )
        .onReceive(vm.registerSuccess) { onRegistered() }
    }
}

private struct RegisterContent: View {
    let form: RegisterFormState
    let isLoading: Bool
    let onFirstNameChange: (String) -> Void
    let onLastNameChange: (String) -> Void
    let onEmailChange: (String) -> Void
    let onPasswordChange: (String) -> Void
    let onConfirmPasswordChange: (String) -> Void
    let onAcceptTermsChange: (Bool) -> Void
    let onSignIn: () -> Void
    let onSubmit: () -> Void

    private func binding(_ value: String, _ setter: @escaping (String) -> Void) -> Binding<String> {
        Binding(get: { value }, set: setter)
    }

    private var acceptTermsBinding: Binding<Bool> {
        Binding(get: { form.acceptTerms }, set: onAcceptTermsChange)
    }

    var body: some View {
        ScrollView {
            VStack(spacing: 0) {
                Mascot.waving.image
                    .resizable()
                    .scaledToFit()
                    .frame(width: 140, height: 140)

                Spacer().frame(height: Spacing.l)

                Text(L10n.Register.title)
                    .font(CleansiaTypography.displayMedium)
                    .foregroundColor(CleansiaColors.onBackground)
                    .multilineTextAlignment(.center)

                Spacer().frame(height: Spacing.xs)

                Text(L10n.Register.subtitle)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)

                Spacer().frame(height: Spacing.xl)

                HStack(spacing: Spacing.s) {
                    CleansiaTextField(
                        value: binding(form.firstName, onFirstNameChange),
                        label: L10n.Register.firstName,
                        errorText: form.firstNameError,
                        enabled: !isLoading
                    )
                    CleansiaTextField(
                        value: binding(form.lastName, onLastNameChange),
                        label: L10n.Register.lastName,
                        errorText: form.lastNameError,
                        enabled: !isLoading
                    )
                }

                Spacer().frame(height: Spacing.xs)

                CleansiaTextField(
                    value: binding(form.email, onEmailChange),
                    label: L10n.email,
                    errorText: form.emailError,
                    keyboardType: .emailAddress,
                    enabled: !isLoading
                )

                Spacer().frame(height: Spacing.xs)

                CleansiaTextField(
                    value: binding(form.password, onPasswordChange),
                    label: L10n.password,
                    errorText: form.passwordError,
                    isPassword: true,
                    enabled: !isLoading
                )

                PasswordRuleList(
                    rules: [
                        PasswordRule(label: L10n.Register.ruleMinLength, isSatisfied: form.passwordHasMinLength),
                        PasswordRule(label: L10n.Register.ruleLetter, isSatisfied: form.passwordHasLetter),
                        PasswordRule(label: L10n.Register.ruleNumber, isSatisfied: form.passwordHasNumber)
                    ],
                    hasInput: !form.password.isEmpty
                )

                Spacer().frame(height: Spacing.xs)

                CleansiaTextField(
                    value: binding(form.confirmPassword, onConfirmPasswordChange),
                    label: L10n.Register.confirmPassword,
                    errorText: form.confirmPasswordError,
                    isPassword: true,
                    enabled: !isLoading
                )

                PasswordRuleList(
                    rules: [
                        PasswordRule(label: L10n.Register.ruleMatch, isSatisfied: form.passwordsMatch)
                    ],
                    hasInput: !form.confirmPassword.isEmpty
                )

                Spacer().frame(height: Spacing.s)

                VStack(alignment: .leading, spacing: Spacing.xxs) {
                    CleansiaCheckbox(checked: acceptTermsBinding, label: L10n.Register.acceptTerms)
                    if let termsError = form.termsError {
                        Text(termsError)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.error)
                            .padding(.horizontal, Spacing.xxs)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)

                Spacer().frame(height: Spacing.m)

                CleansiaPrimaryButton(
                    L10n.Register.submit,
                    loading: isLoading,
                    enabled: form.isValid,
                    action: onSubmit
                )

                Spacer().frame(height: Spacing.l)

                HStack(spacing: 0) {
                    Text(L10n.Register.alreadyHaveAccount)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    CleansiaTextLink(L10n.Register.signInHere, action: onSignIn)
                }
            }
            .padding(.horizontal, Spacing.l)
            .padding(.top, 64)
            .padding(.bottom, Spacing.xl)
            .frame(maxWidth: .infinity)
        }
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}

#if DEBUG
    struct RegisterView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                preview(form: RegisterFormState(), isLoading: false)
                    .previewDisplayName("Idle")

                preview(
                    form: RegisterFormState(
                        firstName: "Jana",
                        lastName: "Nováková",
                        email: "jana@b.cz",
                        password: "abcdefg1",
                        confirmPassword: "abcdefg1",
                        acceptTerms: true
                    ),
                    isLoading: true
                )
                .previewDisplayName("Submitting")

                preview(
                    form: RegisterFormState(
                        firstName: "",
                        email: "bad",
                        password: "short",
                        firstNameError: "First name is required",
                        emailError: "Please enter a valid email",
                        passwordError: "Password must be at least 8 characters with a letter and a number",
                        termsError: "You must accept the terms and conditions"
                    ),
                    isLoading: false
                )
                .previewDisplayName("Field errors")
            }
        }

        private static func preview(form: RegisterFormState, isLoading: Bool) -> some View {
            RegisterContent(
                form: form,
                isLoading: isLoading,
                onFirstNameChange: { _ in },
                onLastNameChange: { _ in },
                onEmailChange: { _ in },
                onPasswordChange: { _ in },
                onConfirmPasswordChange: { _ in },
                onAcceptTermsChange: { _ in },
                onSignIn: {},
                onSubmit: {}
            )
        }
    }
#endif
