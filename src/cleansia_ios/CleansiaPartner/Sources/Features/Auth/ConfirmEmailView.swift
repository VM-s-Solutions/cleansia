import CleansiaCore
import SwiftUI

private let confirmCodeLength = 6

struct ConfirmEmailView: View {
    @StateObject private var vm: ConfirmEmailViewModel
    let onBack: () -> Void
    let onConfirmed: () -> Void

    init(
        email: String?,
        registeredPassword: String?,
        client: EmailConfirmationClient,
        settings: AppSettingsStore,
        snackbar: SnackbarController,
        onBack: @escaping () -> Void,
        onConfirmed: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: ConfirmEmailViewModel(
            email: email,
            registeredPassword: registeredPassword,
            client: client,
            settings: settings,
            snackbar: snackbar
        ))
        self.onBack = onBack
        self.onConfirmed = onConfirmed
    }

    var body: some View {
        ConfirmEmailContent(
            code: vm.code,
            passwordForm: vm.asksForPassword ? vm.passwordForm : nil,
            isConfirming: vm.confirmState.isSubmitting,
            isResending: vm.resendState.isSubmitting,
            canResend: vm.canResend,
            onCodeChange: vm.onCodeChange,
            onPasswordChange: vm.onPasswordChange,
            onConfirmPasswordChange: vm.onConfirmPasswordChange,
            onBack: onBack,
            onConfirm: { Task { await vm.confirmEmail() } },
            onResend: { Task { await vm.resendCode() } }
        )
        .onChange(of: vm.code) { _ in
            if vm.isReadyToConfirm, !vm.confirmState.isSubmitting {
                Task { await vm.confirmEmail() }
            }
        }
        .onReceive(vm.confirmSuccess) { onConfirmed() }
    }
}

private struct ConfirmEmailContent: View {
    let code: String
    /// Nil when the confirmation sets the password just typed on the registration.
    let passwordForm: ChosenPassword?
    let isConfirming: Bool
    let isResending: Bool
    let canResend: Bool
    let onCodeChange: (String) -> Void
    var onPasswordChange: (String) -> Void = { _ in }
    var onConfirmPasswordChange: (String) -> Void = { _ in }
    let onBack: () -> Void
    let onConfirm: () -> Void
    let onResend: () -> Void

    private var codeBinding: Binding<String> {
        Binding(get: { code }, set: onCodeChange)
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Button(action: onBack) {
                    Image(systemName: "chevron.backward")
                        .font(.system(size: 18, weight: .semibold))
                        .foregroundColor(CleansiaColors.onBackground)
                }
                .accessibilityLabel(L10n.ConfirmEmail.back)
                Spacer()
            }
            .padding(Spacing.s)

            CenteredAuthScroll {
                VStack(spacing: 0) {
                    Text(L10n.ConfirmEmail.title)
                        .cleansiaFont(CleansiaTypography.displayMedium)
                        .foregroundColor(CleansiaColors.onBackground)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xs)

                    Text(L10n.ConfirmEmail.subtitle)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xl)

                    CodeInput(code: codeBinding, length: confirmCodeLength, showsPasteAction: false)

                    if let passwordForm {
                        Spacer().frame(height: Spacing.l)

                        ChosenPasswordFields(
                            form: passwordForm,
                            enabled: !isConfirming,
                            onPasswordChange: onPasswordChange,
                            onConfirmPasswordChange: onConfirmPasswordChange
                        )
                    }

                    Spacer().frame(height: Spacing.l)

                    CleansiaPrimaryButton(
                        L10n.ConfirmEmail.verify,
                        trailingIcon: "checkmark.circle",
                        loading: isConfirming,
                        enabled: code.count == confirmCodeLength,
                        action: onConfirm
                    )

                    Spacer().frame(height: Spacing.xs)

                    CleansiaOutlinedButton(
                        L10n.ConfirmEmail.resendCode,
                        leadingIcon: "arrow.clockwise",
                        enabled: canResend && !isResending && !isConfirming,
                        action: onResend
                    )
                }
                .padding(.horizontal, Spacing.l)
                .padding(.vertical, Spacing.xl)
                .frame(maxWidth: .infinity)
            }
        }
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}

private struct ChosenPasswordFields: View {
    let form: ChosenPassword
    let enabled: Bool
    let onPasswordChange: (String) -> Void
    let onConfirmPasswordChange: (String) -> Void

    var body: some View {
        VStack(spacing: 0) {
            Text(L10n.ConfirmEmail.choosePassword)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
                .frame(maxWidth: .infinity)

            Spacer().frame(height: Spacing.s)

            CleansiaTextField(
                value: Binding(get: { form.password }, set: onPasswordChange),
                label: L10n.password,
                errorText: form.passwordError,
                textContentType: .newPassword,
                isPassword: true,
                enabled: enabled
            )

            PasswordRuleList(
                rules: [
                    PasswordRule(label: L10n.Register.ruleMinLength, isSatisfied: form.hasMinLength),
                    PasswordRule(label: L10n.Register.ruleLetter, isSatisfied: form.hasLetter),
                    PasswordRule(label: L10n.Register.ruleNumber, isSatisfied: form.hasNumber)
                ],
                hasInput: !form.password.isEmpty
            )

            Spacer().frame(height: Spacing.xs)

            CleansiaTextField(
                value: Binding(get: { form.confirmation }, set: onConfirmPasswordChange),
                label: L10n.Register.confirmPassword,
                errorText: form.confirmationError,
                textContentType: .newPassword,
                isPassword: true,
                enabled: enabled
            )

            PasswordRuleList(
                rules: [PasswordRule(label: L10n.Register.ruleMatch, isSatisfied: form.matches)],
                hasInput: !form.confirmation.isEmpty
            )
        }
    }
}

#if DEBUG
    struct ConfirmEmailView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                preview(code: "", isConfirming: false, canResend: true)
                    .previewDisplayName("Empty")
                preview(code: "1234", isConfirming: false, canResend: true)
                    .previewDisplayName("Partial")
                preview(code: "123456", isConfirming: true, canResend: false)
                    .previewDisplayName("Submitting")
                preview(code: "", isConfirming: false, canResend: false)
                    .previewDisplayName("No email — resend disabled")
                preview(
                    code: "123456",
                    passwordForm: refusedPassword,
                    isConfirming: false,
                    canResend: true
                )
                .previewDisplayName("Confirming later — asks for the password")
            }
        }

        private static var refusedPassword: ChosenPassword {
            var form = ChosenPassword()
            form.setPassword("short")
            _ = form.validated(
                rulesError: L10n.Register.errorPasswordRules,
                mismatchError: L10n.Register.errorPasswordsNoMatch
            )
            return form
        }

        private static func preview(
            code: String,
            passwordForm: ChosenPassword? = nil,
            isConfirming: Bool,
            canResend: Bool
        ) -> some View {
            ConfirmEmailContent(
                code: code,
                passwordForm: passwordForm,
                isConfirming: isConfirming,
                isResending: false,
                canResend: canResend,
                onCodeChange: { _ in },
                onBack: {},
                onConfirm: {},
                onResend: {}
            )
        }
    }
#endif
