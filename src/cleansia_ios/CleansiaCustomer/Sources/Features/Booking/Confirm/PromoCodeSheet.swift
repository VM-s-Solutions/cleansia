import CleansiaCore
import SwiftUI

struct PromoCodeSheet: View {
    let initialCode: String
    let currencyCode: String
    let onValidate: (String) async -> PromoCodeState
    let onDismiss: () -> Void

    @Environment(\.snackbarController) private var snackbar
    @State private var code: String
    @State private var localState: PromoCodeState = .idle

    init(
        initialCode: String,
        currencyCode: String,
        onValidate: @escaping (String) async -> PromoCodeState,
        onDismiss: @escaping () -> Void
    ) {
        self.initialCode = initialCode
        self.currencyCode = currencyCode
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
            title: L10n.Booking.promoDialogTitle,
            code: $code,
            border: border,
            isError: isError,
            isSubmitting: isSubmitting,
            isValid: isValid,
            applyTitle: L10n.Booking.promoDialogApply,
            cancelTitle: L10n.Booking.promoDialogCancel,
            doneTitle: L10n.Booking.promoDialogDone,
            onEdit: resetIfResolved,
            onApply: apply,
            onDone: onDismiss,
            onCancel: onDismiss,
            message: { resultBlock }
        )
        .snackbarHost(snackbar, bottomInset: SnackbarController.defaultBottomInset)
        .presentationDetents([.medium])
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
            CodeSheetMessage.helper(L10n.Booking.promoDialogHelper)
        case .validating:
            CodeSheetMessage.validating(L10n.Booking.promoValidating)
        case let .valid(amount):
            CodeSheetMessage.success(L10n.Booking.promoDialogSuccess(BookingPricing.formatTotal(
                amount,
                currencyCode: currencyCode
            )))
        case let .invalid(error):
            CodeSheetMessage.error(L10n.Booking.promoError(error))
        }
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
