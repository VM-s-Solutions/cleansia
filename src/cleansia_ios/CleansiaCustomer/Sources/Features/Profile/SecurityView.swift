import CleansiaCore
import SwiftUI

struct SecurityView: View {
    @StateObject private var vm: SecurityViewModel
    @State private var code = ""
    @State private var newPassword = ""
    @State private var confirmPassword = ""

    private let onChanged: () -> Void

    init(
        email: String,
        language: String,
        client: ChangePasswordClient,
        snackbar: SnackbarController,
        onChanged: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: SecurityViewModel(
            email: email,
            language: language,
            client: client,
            snackbar: snackbar
        ))
        self.onChanged = onChanged
    }

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.l) {
                    Text(L10n.Security.changePassword)
                        .font(CleansiaTypography.titleLarge)
                        .foregroundColor(CleansiaColors.onSurface)

                    Text(L10n.Security.intro)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)

                    if vm.codeRequested {
                        changeForm
                    } else {
                        requestStep
                    }
                }
                .padding(Spacing.m)
            }
        }
        .navigationTitle(L10n.Security.title)
        .navigationBarTitleDisplayMode(.inline)
        .onReceive(vm.passwordChanged) { onChanged() }
    }

    private var requestStep: some View {
        CleansiaPrimaryButton(
            L10n.Security.requestCode,
            loading: vm.requestState.isSubmitting,
            enabled: !vm.email.isBlank && !vm.requestState.isSubmitting
        ) {
            Task { await vm.requestCode() }
        }
    }

    private var changeForm: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            Text(L10n.Security.codeHelper)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            CleansiaTextField(value: $code, label: L10n.Security.codeLabel, keyboardType: .numberPad)
            CleansiaTextField(value: $newPassword, label: L10n.Security.newPassword, isPassword: true)
            PasswordRuleList(
                rules: [
                    PasswordRule(label: L10n.Auth.ruleMinLength, isSatisfied: PasswordPolicy.hasMinLength(newPassword)),
                    PasswordRule(label: L10n.Auth.ruleLetter, isSatisfied: PasswordPolicy.hasLetter(newPassword)),
                    PasswordRule(label: L10n.Auth.ruleNumber, isSatisfied: PasswordPolicy.hasNumber(newPassword))
                ],
                hasInput: !newPassword.isEmpty
            )
            CleansiaTextField(value: $confirmPassword, label: L10n.Security.confirmPassword, isPassword: true)
            CleansiaPrimaryButton(
                L10n.Security.updateButton,
                loading: vm.changeState.isSubmitting,
                enabled: canSubmit
            ) {
                Task { await vm.changePassword(code: code, newPassword: newPassword, confirmPassword: confirmPassword) }
            }
        }
    }

    private var canSubmit: Bool {
        !code.isBlank
            && PasswordPolicy.isValid(newPassword)
            && PasswordPolicy.passwordsMatch(newPassword, confirmPassword)
            && !vm.changeState.isSubmitting
    }
}
