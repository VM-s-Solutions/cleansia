import CleansiaCore
import SwiftUI

struct ForgotPasswordView: View {
    @StateObject private var vm: ForgotPasswordViewModel
    let onBack: () -> Void
    let onRequested: () -> Void

    init(
        client: PasswordResetClient,
        settings: AppSettingsStore,
        snackbar: SnackbarController,
        onBack: @escaping () -> Void,
        onRequested: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: ForgotPasswordViewModel(
            client: client,
            settings: settings,
            snackbar: snackbar
        ))
        self.onBack = onBack
        self.onRequested = onRequested
    }

    var body: some View {
        ForgotPasswordContent(
            form: vm.form,
            isLoading: vm.requestState.isSubmitting,
            onEmailChange: vm.onEmailChange,
            onBack: onBack,
            onSignIn: onBack,
            onSubmit: { Task { await vm.submit() } }
        )
        .onReceive(vm.requestSuccess) { onRequested() }
    }
}

private struct ForgotPasswordContent: View {
    let form: ForgotPasswordFormState
    let isLoading: Bool
    let onEmailChange: (String) -> Void
    let onBack: () -> Void
    let onSignIn: () -> Void
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
                .accessibilityLabel(L10n.ForgotPassword.back)
                Spacer()
            }
            .padding(Spacing.s)

            ScrollView {
                VStack(spacing: 0) {
                    Mascot.waving.image
                        .resizable()
                        .scaledToFit()
                        .frame(width: 140, height: 140)

                    Spacer().frame(height: Spacing.l)

                    Text(L10n.ForgotPassword.title)
                        .font(CleansiaTypography.displayMedium)
                        .foregroundColor(CleansiaColors.onBackground)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xs)

                    Text(L10n.ForgotPassword.subtitle)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xl)

                    CleansiaTextField(
                        value: emailBinding,
                        label: L10n.email,
                        errorText: form.emailError,
                        keyboardType: .emailAddress,
                        enabled: !isLoading
                    )

                    Spacer().frame(height: Spacing.m)

                    CleansiaPrimaryButton(
                        L10n.ForgotPassword.reset,
                        loading: isLoading,
                        enabled: canSubmit,
                        action: onSubmit
                    )

                    Spacer().frame(height: Spacing.l)

                    HStack(spacing: 0) {
                        Text(L10n.ForgotPassword.alreadyHaveAccount)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                        CleansiaTextLink(L10n.ForgotPassword.signInHere, action: onSignIn)
                    }
                }
                .padding(.horizontal, Spacing.l)
                .padding(.bottom, Spacing.xl)
                .frame(maxWidth: .infinity)
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
                onSignIn: {},
                onSubmit: {}
            )
        }
    }
#endif
