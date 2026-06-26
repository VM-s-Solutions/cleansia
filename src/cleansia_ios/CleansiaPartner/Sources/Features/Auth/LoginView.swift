import CleansiaCore
import SwiftUI

struct LoginView: View {
    @StateObject private var vm: LoginViewModel
    let onSignUp: () -> Void
    let onLoginSuccess: (LoginSuccess) -> Void

    init(
        loginClient: LoginClient,
        snackbar: SnackbarController,
        onSignUp: @escaping () -> Void,
        onLoginSuccess: @escaping (LoginSuccess) -> Void
    ) {
        _vm = StateObject(wrappedValue: LoginViewModel(loginClient: loginClient, snackbar: snackbar))
        self.onSignUp = onSignUp
        self.onLoginSuccess = onLoginSuccess
    }

    var body: some View {
        LoginContent(
            form: vm.form,
            isLoading: vm.loginState.isSubmitting,
            onEmailChange: vm.onEmailChange,
            onPasswordChange: vm.onPasswordChange,
            onRememberMeChange: vm.onRememberMeChange,
            onForgotPassword: {},
            onSignUp: onSignUp,
            onSubmit: { Task { await vm.login() } }
        )
        .onReceive(vm.loginSuccess) { onLoginSuccess($0) }
    }
}

private struct LoginContent: View {
    let form: LoginFormState
    let isLoading: Bool
    let onEmailChange: (String) -> Void
    let onPasswordChange: (String) -> Void
    let onRememberMeChange: (Bool) -> Void
    let onForgotPassword: () -> Void
    let onSignUp: () -> Void
    let onSubmit: () -> Void

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
        ScrollView {
            VStack(spacing: 0) {
                Mascot.waving.image
                    .resizable()
                    .scaledToFit()
                    .frame(width: 160, height: 160)

                Spacer().frame(height: Spacing.l)

                Text(L10n.welcomeBack)
                    .font(CleansiaTypography.displayMedium)
                    .foregroundColor(CleansiaColors.onBackground)
                    .multilineTextAlignment(.center)

                Spacer().frame(height: Spacing.xs)

                Text(L10n.loginSubtitle)
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

                Spacer().frame(height: Spacing.xs)

                CleansiaTextField(
                    value: passwordBinding,
                    label: L10n.password,
                    errorText: form.passwordError,
                    isPassword: true,
                    enabled: !isLoading
                )

                Spacer().frame(height: Spacing.xs)

                HStack {
                    CleansiaCheckbox(checked: rememberMeBinding, label: L10n.rememberMe)
                    Spacer()
                    CleansiaTextLink(L10n.forgotPassword, action: onForgotPassword)
                }

                Spacer().frame(height: Spacing.m)

                CleansiaPrimaryButton(
                    L10n.login,
                    loading: isLoading,
                    enabled: canSubmit,
                    action: onSubmit
                )

                Spacer().frame(height: Spacing.l)

                HStack(spacing: 0) {
                    Text(L10n.dontHaveAccount)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    CleansiaTextLink(L10n.signUpHere, action: onSignUp)
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
    struct LoginView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                preview(
                    form: LoginFormState(email: "", password: ""),
                    isLoading: false
                )
                .previewDisplayName("Idle")

                preview(
                    form: LoginFormState(email: "a@b.com", password: "secret"),
                    isLoading: true
                )
                .previewDisplayName("Submitting")

                preview(
                    form: LoginFormState(
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

        private static func preview(form: LoginFormState, isLoading: Bool) -> some View {
            PreviewStateWrapper(form) { binding in
                LoginContent(
                    form: binding.wrappedValue,
                    isLoading: isLoading,
                    onEmailChange: { binding.wrappedValue.email = $0 },
                    onPasswordChange: { binding.wrappedValue.password = $0 },
                    onRememberMeChange: { binding.wrappedValue.rememberMe = $0 },
                    onForgotPassword: {},
                    onSignUp: {},
                    onSubmit: {}
                )
            }
        }
    }

    private struct PreviewStateWrapper<Value, Content: View>: View {
        @State private var value: Value
        private let content: (Binding<Value>) -> Content

        init(_ initialValue: Value, @ViewBuilder content: @escaping (Binding<Value>) -> Content) {
            _value = State(initialValue: initialValue)
            self.content = content
        }

        var body: some View {
            content($value)
        }
    }
#endif
