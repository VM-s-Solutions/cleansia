import CleansiaCore
import SwiftUI

struct ForgotPasswordView: View {
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
        ForgotPasswordContent(
            form: vm.forgotForm,
            isLoading: vm.forgotState.isSubmitting,
            codeSent: vm.resetCodeSent,
            resetState: vm.resetState,
            onEmailChange: vm.onForgotEmailChange,
            onBack: onBack,
            onSubmit: { Task { await vm.requestPasswordReset() } },
            onCompleteReset: { code, newPassword, confirmPassword in
                Task {
                    await vm.completePasswordReset(
                        code: code,
                        newPassword: newPassword,
                        confirmPassword: confirmPassword
                    )
                }
            }
        )
        .onReceive(vm.outcome) { onOutcome($0) }
    }
}

private struct ForgotPasswordContent: View {
    let form: ForgotPasswordFormState
    let isLoading: Bool
    let codeSent: Bool
    let resetState: ActionState
    let onEmailChange: (String) -> Void
    let onBack: () -> Void
    let onSubmit: () -> Void
    let onCompleteReset: (String, String, String) -> Void

    @State private var code = ""
    @State private var newPassword = ""
    @State private var confirmPassword = ""

    private var emailBinding: Binding<String> {
        Binding(get: { form.email }, set: onEmailChange)
    }

    private var canSubmit: Bool {
        !form.email.isBlank && !isLoading
    }

    private var canCompleteReset: Bool {
        !code.isBlank
            && PasswordPolicy.isValid(newPassword)
            && PasswordPolicy.passwordsMatch(newPassword, confirmPassword)
            && !resetState.isSubmitting
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

            CenteredAuthScroll {
                VStack(spacing: 0) {
                    AuthHeaderImage()

                    Spacer().frame(height: Spacing.l)

                    Text(L10n.Auth.forgotTitle)
                        .font(CleansiaTypography.displayMedium)
                        .foregroundColor(CleansiaColors.onBackground)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xs)

                    Text(codeSent ? L10n.Security.codeHelper : L10n.Auth.forgotDescription)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xl)

                    if codeSent {
                        resetStep
                    } else {
                        emailStep
                    }

                    Spacer().frame(height: Spacing.l)

                    HStack(spacing: 0) {
                        Text(L10n.Auth.rememberPassword)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                        CleansiaTextLink(L10n.Auth.signInLink, action: onBack)
                    }
                }
                .padding(.horizontal, Spacing.l)
                .padding(.vertical, Spacing.xl)
            }
        }
        .background(CleansiaColors.background.ignoresSafeArea())
    }

    private var emailStep: some View {
        VStack(spacing: 0) {
            CleansiaTextField(
                value: emailBinding,
                label: L10n.Auth.email,
                errorText: form.emailError,
                keyboardType: .emailAddress,
                textContentType: .emailAddress,
                enabled: !isLoading
            )

            Spacer().frame(height: Spacing.m)

            CleansiaPrimaryButton(
                L10n.Auth.forgotSendCode,
                loading: isLoading,
                enabled: canSubmit,
                action: onSubmit
            )
        }
    }

    /// The second half of the reset, lifted from `SecurityView.changeForm` — same endpoint,
    /// same rules, same strings, so the signed-out and signed-in password changes look and
    /// behave identically.
    private var resetStep: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            CleansiaTextField(
                value: $code,
                label: L10n.Security.codeLabel,
                keyboardType: .numberPad,
                textContentType: .oneTimeCode,
                enabled: !resetState.isSubmitting
            )
            CleansiaTextField(
                value: $newPassword,
                label: L10n.Security.newPassword,
                textContentType: .newPassword,
                isPassword: true,
                enabled: !resetState.isSubmitting
            )
            PasswordRuleList(
                rules: [
                    PasswordRule(label: L10n.Auth.ruleMinLength, isSatisfied: PasswordPolicy.hasMinLength(newPassword)),
                    PasswordRule(label: L10n.Auth.ruleLetter, isSatisfied: PasswordPolicy.hasLetter(newPassword)),
                    PasswordRule(label: L10n.Auth.ruleNumber, isSatisfied: PasswordPolicy.hasNumber(newPassword))
                ],
                hasInput: !newPassword.isEmpty
            )
            CleansiaTextField(
                value: $confirmPassword,
                label: L10n.Security.confirmPassword,
                textContentType: .newPassword,
                isPassword: true,
                enabled: !resetState.isSubmitting
            )
            if let message = resetState.errorMessage {
                Text(message)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.error)
                    .multilineTextAlignment(.leading)
            }
            CleansiaPrimaryButton(
                L10n.Security.updateButton,
                loading: resetState.isSubmitting,
                enabled: canCompleteReset
            ) {
                onCompleteReset(code, newPassword, confirmPassword)
            }
        }
    }
}

#if DEBUG
    struct ForgotPasswordView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                preview(form: ForgotPasswordFormState(), isLoading: false)
                    .previewDisplayName("Idle")
                preview(form: ForgotPasswordFormState(email: "a@b.cz"), isLoading: true)
                    .previewDisplayName("Submitting")
                preview(
                    form: ForgotPasswordFormState(email: "bad", emailError: "Please enter a valid email"),
                    isLoading: false
                )
                .previewDisplayName("Field error")
                preview(form: ForgotPasswordFormState(email: "a@b.cz"), isLoading: false, codeSent: true)
                    .previewDisplayName("Code sent")
                preview(
                    form: ForgotPasswordFormState(email: "a@b.cz"),
                    isLoading: false,
                    codeSent: true,
                    resetState: .error("That code is no longer valid")
                )
                .previewDisplayName("Code rejected")
            }
        }

        private static func preview(
            form: ForgotPasswordFormState,
            isLoading: Bool,
            codeSent: Bool = false,
            resetState: ActionState = .idle
        ) -> some View {
            ForgotPasswordContent(
                form: form,
                isLoading: isLoading,
                codeSent: codeSent,
                resetState: resetState,
                onEmailChange: { _ in },
                onBack: {},
                onSubmit: {},
                onCompleteReset: { _, _, _ in }
            )
        }
    }
#endif
