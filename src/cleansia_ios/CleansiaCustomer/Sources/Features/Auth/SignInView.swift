import CleansiaCore
import SwiftUI

struct SignInView: View {
    @StateObject private var vm: CustomerAuthViewModel
    let onForgotPassword: () -> Void
    let onSignUp: () -> Void
    let onOutcome: (AuthOutcome) -> Void

    init(
        makeViewModel: @escaping () -> CustomerAuthViewModel,
        onForgotPassword: @escaping () -> Void,
        onSignUp: @escaping () -> Void,
        onOutcome: @escaping (AuthOutcome) -> Void
    ) {
        _vm = StateObject(wrappedValue: makeViewModel())
        self.onForgotPassword = onForgotPassword
        self.onSignUp = onSignUp
        self.onOutcome = onOutcome
    }

    var body: some View {
        SignInContent(
            form: vm.signInForm,
            isLoading: vm.signInState.isSubmitting,
            isSocialLoading: vm.socialState.isSubmitting,
            onEmailChange: vm.onSignInEmailChange,
            onPasswordChange: vm.onSignInPasswordChange,
            onRememberMeChange: vm.onRememberMeChange,
            onForgotPassword: onForgotPassword,
            onSignUp: onSignUp,
            onSubmit: { Task { await vm.signIn() } },
            onApple: { Task { await vm.signInWithApple() } },
            onGoogle: { Task { await vm.signInWithGoogle() } }
        )
        .onReceive(vm.outcome) { onOutcome($0) }
    }
}

private struct SignInContent: View {
    let form: SignInFormState
    let isLoading: Bool
    let isSocialLoading: Bool
    let onEmailChange: (String) -> Void
    let onPasswordChange: (String) -> Void
    let onRememberMeChange: (Bool) -> Void
    let onForgotPassword: () -> Void
    let onSignUp: () -> Void
    let onSubmit: () -> Void
    let onApple: () -> Void
    let onGoogle: () -> Void

    private var emailBinding: Binding<String> {
        Binding(get: { form.email }, set: onEmailChange)
    }

    private var passwordBinding: Binding<String> {
        Binding(get: { form.password }, set: onPasswordChange)
    }

    private var rememberMeBinding: Binding<Bool> {
        Binding(get: { form.rememberMe }, set: onRememberMeChange)
    }

    private var canSubmit: Bool {
        !form.email.isBlank && !form.password.isBlank
    }

    var body: some View {
        CenteredAuthScroll {
            VStack(spacing: 0) {
                AuthHeaderImage(size: 160)

                Spacer().frame(height: Spacing.l)

                Text(L10n.Auth.signInTitle)
                    .font(CleansiaTypography.displayMedium)
                    .foregroundColor(CleansiaColors.onBackground)
                    .multilineTextAlignment(.center)

                Spacer().frame(height: Spacing.xs)

                Text(L10n.Auth.signInSubtitle)
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

                Spacer().frame(height: Spacing.xs)

                CleansiaTextField(
                    value: passwordBinding,
                    label: L10n.Auth.password,
                    errorText: form.passwordError,
                    isPassword: true,
                    enabled: !isLoading
                )

                Spacer().frame(height: Spacing.xs)

                HStack {
                    CleansiaCheckbox(checked: rememberMeBinding, label: L10n.Auth.rememberMe)
                    Spacer()
                    CleansiaTextLink(L10n.Auth.forgotPassword, action: onForgotPassword)
                }

                Spacer().frame(height: Spacing.m)

                CleansiaPrimaryButton(
                    L10n.Auth.signIn,
                    loading: isLoading,
                    enabled: canSubmit,
                    action: onSubmit
                )

                Spacer().frame(height: Spacing.m)

                SocialSignInSection(isLoading: isSocialLoading, onApple: onApple, onGoogle: onGoogle)

                Spacer().frame(height: Spacing.l)

                HStack(spacing: 0) {
                    Text(L10n.Auth.dontHaveAccount)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    CleansiaTextLink(L10n.Auth.signUpLink, action: onSignUp)
                }
            }
            .padding(.horizontal, Spacing.l)
            .padding(.vertical, Spacing.xl)
        }
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}

#if DEBUG
    struct SignInView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                preview(form: SignInFormState(), isLoading: false)
                    .previewDisplayName("Idle")
                preview(form: SignInFormState(email: "a@b.cz", password: "secret"), isLoading: true)
                    .previewDisplayName("Submitting")
                preview(
                    form: SignInFormState(
                        email: "bad",
                        password: "",
                        emailError: "Please enter a valid email",
                        passwordError: "Password is required"
                    ),
                    isLoading: false
                )
                .previewDisplayName("Field error")
            }
        }

        private static func preview(form: SignInFormState, isLoading: Bool) -> some View {
            SignInContent(
                form: form,
                isLoading: isLoading,
                isSocialLoading: false,
                onEmailChange: { _ in },
                onPasswordChange: { _ in },
                onRememberMeChange: { _ in },
                onForgotPassword: {},
                onSignUp: {},
                onSubmit: {},
                onApple: {},
                onGoogle: {}
            )
        }
    }
#endif
