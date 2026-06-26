import CleansiaCore
import SwiftUI

private let confirmCodeLength = 6

struct ConfirmEmailView: View {
    @StateObject private var vm: ConfirmEmailViewModel
    let onBack: () -> Void
    let onConfirmed: () -> Void

    init(
        email: String?,
        client: EmailConfirmationClient,
        settings: AppSettingsStore,
        snackbar: SnackbarController,
        onBack: @escaping () -> Void,
        onConfirmed: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: ConfirmEmailViewModel(
            email: email,
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
            isConfirming: vm.confirmState.isSubmitting,
            isResending: vm.resendState.isSubmitting,
            canResend: vm.canResend,
            onCodeChange: vm.onCodeChange,
            onBack: onBack,
            onConfirm: { Task { await vm.confirmEmail() } },
            onResend: { Task { await vm.resendCode() } }
        )
        .onChange(of: vm.code) { value in
            if value.count == confirmCodeLength, !vm.confirmState.isSubmitting {
                Task { await vm.confirmEmail() }
            }
        }
        .onReceive(vm.confirmSuccess) { onConfirmed() }
    }
}

private struct ConfirmEmailContent: View {
    let code: String
    let isConfirming: Bool
    let isResending: Bool
    let canResend: Bool
    let onCodeChange: (String) -> Void
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

            ScrollView {
                VStack(spacing: 0) {
                    Mascot.waving.image
                        .resizable()
                        .scaledToFit()
                        .frame(width: 140, height: 140)

                    Spacer().frame(height: Spacing.l)

                    Text(L10n.ConfirmEmail.title)
                        .font(CleansiaTypography.displayMedium)
                        .foregroundColor(CleansiaColors.onBackground)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xs)

                    Text(L10n.ConfirmEmail.subtitle)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xl)

                    CodeInput(code: codeBinding, length: confirmCodeLength)

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
                .padding(.bottom, Spacing.xl)
                .frame(maxWidth: .infinity)
            }
        }
        .background(CleansiaColors.background.ignoresSafeArea())
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
            }
        }

        private static func preview(code: String, isConfirming: Bool, canResend: Bool) -> some View {
            ConfirmEmailContent(
                code: code,
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
