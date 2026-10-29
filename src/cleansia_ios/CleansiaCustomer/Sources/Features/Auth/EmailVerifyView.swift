import CleansiaCore
import SwiftUI

struct EmailVerifyView: View {
    @StateObject private var vm: CustomerAuthViewModel
    let onBack: () -> Void
    let onOutcome: (AuthOutcome) -> Void

    init(
        makeViewModel: @escaping () -> CustomerAuthViewModel,
        onBack: @escaping () -> Void,
        onOutcome: @escaping (AuthOutcome) -> Void
    ) {
        _vm = StateObject(wrappedValue: makeViewModel())
        self.onBack = onBack
        self.onOutcome = onOutcome
    }

    var body: some View {
        EmailVerifyContent(
            code: vm.verifyCode,
            passwordForm: vm.asksForVerifyPassword ? vm.verifyPasswordForm : nil,
            isConfirming: vm.confirmState.isSubmitting,
            isResending: vm.resendState.isSubmitting,
            canResend: vm.canResend,
            onCodeChange: vm.onVerifyCodeChange,
            onPasswordChange: vm.onVerifyPasswordChange,
            onConfirmPasswordChange: vm.onVerifyConfirmPasswordChange,
            onBack: onBack,
            onConfirm: { Task { await vm.confirmEmail() } },
            onResend: { Task { await vm.resendCode() } }
        )
        .onChange(of: vm.verifyCode) { _ in
            if vm.isVerifyReadyToConfirm, !vm.confirmState.isSubmitting {
                Task { await vm.confirmEmail() }
            }
        }
        .onReceive(vm.outcome) { onOutcome($0) }
    }
}

private struct EmailVerifyContent: View {
    let code: String
    /// Nil when the confirmation sets the password just typed on the sign-up.
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
                .accessibilityLabel(L10n.Auth.back)
                Spacer()
            }
            .padding(Spacing.s)

            // The other auth screens' container: centred when it fits, scrolling (without a rubber band)
            // when the keyboard or a large text size makes it not.
            CenteredAuthScroll {
                VStack(spacing: 0) {
                    Text(L10n.Auth.verifyTitle)
                        .cleansiaFont(CleansiaTypography.displayMedium)
                        .foregroundColor(CleansiaColors.onBackground)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xs)

                    Text(L10n.Auth.verifyDescription)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xl)

                    CodeInput(code: codeBinding, length: confirmCodeLength, showsPasteAction: false)

                    if let passwordForm {
                        Spacer().frame(height: Spacing.l)

                        VerifyPasswordFields(
                            form: passwordForm,
                            enabled: !isConfirming,
                            onPasswordChange: onPasswordChange,
                            onConfirmPasswordChange: onConfirmPasswordChange
                        )
                    }

                    Spacer().frame(height: Spacing.l)

                    CleansiaPrimaryButton(
                        L10n.Auth.verifySubmit,
                        trailingIcon: "checkmark.circle",
                        loading: isConfirming,
                        enabled: code.count == confirmCodeLength,
                        action: onConfirm
                    )

                    Spacer().frame(height: Spacing.xs)

                    CleansiaOutlinedButton(
                        L10n.Auth.verifyResendCode,
                        leadingIcon: "arrow.clockwise",
                        enabled: canResend && !isResending && !isConfirming,
                        action: onResend
                    )
                }
                .padding(.horizontal, Spacing.l)
                .padding(.bottom, Spacing.xl)
                .frame(maxWidth: .infinity)
            }
        }
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}

private struct VerifyPasswordFields: View {
    let form: ChosenPassword
    let enabled: Bool
    let onPasswordChange: (String) -> Void
    let onConfirmPasswordChange: (String) -> Void

    var body: some View {
        VStack(spacing: 0) {
            Text(L10n.Auth.verifyChoosePassword)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
                .frame(maxWidth: .infinity)

            Spacer().frame(height: Spacing.s)

            CleansiaTextField(
                value: Binding(get: { form.password }, set: onPasswordChange),
                label: L10n.Auth.password,
                errorText: form.passwordError,
                textContentType: .newPassword,
                isPassword: true,
                enabled: enabled
            )

            PasswordRuleList(
                rules: [
                    PasswordRule(label: L10n.Auth.ruleMinLength, isSatisfied: form.hasMinLength),
                    PasswordRule(label: L10n.Auth.ruleLetter, isSatisfied: form.hasLetter),
                    PasswordRule(label: L10n.Auth.ruleNumber, isSatisfied: form.hasNumber)
                ],
                hasInput: !form.password.isEmpty
            )

            Spacer().frame(height: Spacing.xs)

            CleansiaTextField(
                value: Binding(get: { form.confirmation }, set: onConfirmPasswordChange),
                label: L10n.Auth.confirmPassword,
                errorText: form.confirmationError,
                textContentType: .newPassword,
                isPassword: true,
                enabled: enabled
            )

            PasswordRuleList(
                rules: [PasswordRule(label: L10n.Auth.ruleMatch, isSatisfied: form.matches)],
                hasInput: !form.confirmation.isEmpty
            )
        }
    }
}

#if DEBUG
    struct EmailVerifyView_Previews: PreviewProvider {
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
            _ = form.validated(rulesError: L10n.Auth.errorPasswordRules, mismatchError: L10n.Auth.errorPasswordsNoMatch)
            return form
        }

        private static func preview(
            code: String,
            passwordForm: ChosenPassword? = nil,
            isConfirming: Bool,
            canResend: Bool
        ) -> some View {
            EmailVerifyContent(
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
