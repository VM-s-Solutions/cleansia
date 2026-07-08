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
            onEmailChange: vm.onForgotEmailChange,
            onBack: onBack,
            onSubmit: { Task { await vm.requestPasswordReset() } }
        )
        .onReceive(vm.outcome) { onOutcome($0) }
    }
}

private struct ForgotPasswordContent: View {
    let form: ForgotPasswordFormState
    let isLoading: Bool
    let onEmailChange: (String) -> Void
    let onBack: () -> Void
    let onSubmit: () -> Void

    private var emailBinding: Binding<String> {
        Binding(get: { form.email }, set: onEmailChange)
    }

    private var canSubmit: Bool {
        !form.email.isBlank && !isLoading
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

                    Text(L10n.Auth.forgotDescription)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xl)

                    CleansiaTextField(
                        value: emailBinding,
                        label: L10n.Auth.email,
                        errorText: form.emailError,
                        keyboardType: .emailAddress,
                        enabled: !isLoading
                    )

                    Spacer().frame(height: Spacing.m)

                    CleansiaPrimaryButton(
                        L10n.Auth.forgotSendCode,
                        loading: isLoading,
                        enabled: canSubmit,
                        action: onSubmit
                    )

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
            }
        }

        private static func preview(form: ForgotPasswordFormState, isLoading: Bool) -> some View {
            ForgotPasswordContent(
                form: form,
                isLoading: isLoading,
                onEmailChange: { _ in },
                onBack: {},
                onSubmit: {}
            )
        }
    }
#endif
