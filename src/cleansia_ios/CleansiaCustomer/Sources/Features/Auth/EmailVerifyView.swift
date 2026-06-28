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
            isConfirming: vm.confirmState.isSubmitting,
            isResending: vm.resendState.isSubmitting,
            canResend: vm.canResend,
            onCodeChange: vm.onVerifyCodeChange,
            onBack: onBack,
            onConfirm: { Task { await vm.confirmEmail() } },
            onResend: { Task { await vm.resendCode() } }
        )
        .onChange(of: vm.verifyCode) { value in
            if value.count == confirmCodeLength, !vm.confirmState.isSubmitting {
                Task { await vm.confirmEmail() }
            }
        }
        .onReceive(vm.outcome) { onOutcome($0) }
    }
}

private struct EmailVerifyContent: View {
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
                .accessibilityLabel(L10n.Auth.back)
                Spacer()
            }
            .padding(Spacing.s)

            ScrollView {
                VStack(spacing: 0) {
                    AuthHeaderImage()

                    Spacer().frame(height: Spacing.l)

                    Text(L10n.Auth.verifyTitle)
                        .font(CleansiaTypography.displayMedium)
                        .foregroundColor(CleansiaColors.onBackground)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xs)

                    Text(L10n.Auth.verifyDescription)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.center)

                    Spacer().frame(height: Spacing.xl)

                    CodeInput(code: codeBinding, length: confirmCodeLength)

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
            }
        }

        private static func preview(code: String, isConfirming: Bool, canResend: Bool) -> some View {
            EmailVerifyContent(
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
