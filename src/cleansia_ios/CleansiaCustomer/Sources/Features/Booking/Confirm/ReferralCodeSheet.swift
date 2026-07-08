import CleansiaCore
import SwiftUI

struct ReferralCodeSheet: View {
    let initialCode: String
    let onValidate: (String) async -> ReferralCodeState
    let onDismiss: () -> Void

    @Environment(\.snackbarController) private var snackbar
    @State private var code: String
    @State private var localState: ReferralCodeState = .idle

    init(
        initialCode: String,
        onValidate: @escaping (String) async -> ReferralCodeState,
        onDismiss: @escaping () -> Void
    ) {
        self.initialCode = initialCode
        self.onValidate = onValidate
        self.onDismiss = onDismiss
        _code = State(initialValue: initialCode.trimmingCharacters(in: .whitespacesAndNewlines).uppercased())
    }

    private var isSubmitting: Bool {
        localState == .validating
    }

    private var isValid: Bool {
        if case .valid = localState { return true }
        return false
    }

    var body: some View {
        CodeSheetShell(
            title: L10n.Booking.referralDialogTitle,
            code: $code,
            border: border,
            isError: isError,
            isSubmitting: isSubmitting,
            isValid: isValid,
            applyTitle: L10n.Booking.referralDialogApply,
            cancelTitle: L10n.Booking.referralDialogCancel,
            doneTitle: L10n.Booking.referralDialogDone,
            onEdit: resetIfResolved,
            onApply: apply,
            onDone: onDismiss,
            onCancel: onDismiss,
            message: { resultBlock }
        )
        .snackbarHost(snackbar, bottomInset: SnackbarController.defaultBottomInset)
    }

    private var isError: Bool {
        if case .invalid = localState { return true }
        return false
    }

    private var border: Color {
        switch localState {
        case .valid: CleansiaColors.primary
        case .invalid: CleansiaColors.error
        default: CleansiaColors.outline
        }
    }

    @ViewBuilder
    private var resultBlock: some View {
        switch localState {
        case .idle:
            CodeSheetMessage.helper(L10n.Booking.referralDialogHelper)
        case .validating:
            CodeSheetMessage.validating(L10n.Booking.promoValidating)
        case let .valid(name):
            CodeSheetMessage.success(successText(name))
        case let .invalid(error):
            CodeSheetMessage.error(L10n.Booking.referralError(error))
        }
    }

    private func successText(_ name: String?) -> String {
        if let name, !name.isBlank {
            return L10n.Booking.referralDialogSuccessNamed(name)
        }
        return L10n.Booking.referralDialogSuccess
    }

    private func resetIfResolved() {
        switch localState {
        case .idle, .validating: break
        default: localState = .idle
        }
    }

    private func apply() {
        Task {
            localState = .validating
            localState = await onValidate(code)
        }
    }
}
