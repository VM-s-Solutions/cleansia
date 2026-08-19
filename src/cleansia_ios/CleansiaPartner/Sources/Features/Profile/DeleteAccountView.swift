import CleansiaCore
import SwiftUI

/// Request account deletion — the partner counterpart of `CleansiaCustomer`'s `DeleteAccountView`,
/// and deliberately a separate screen rather than a shared one. → /decisions/adr-0052
///
/// Three things differ from the customer screen, and each is the point:
///
/// 1. **It asks, it does not delete.** The CTA files a request an admin fulfils once the
///    cooperation has been formally ended and the paperwork signed. Announcing a deletion here
///    would be a lie the endpoint used to be able to tell truthfully.
/// 2. **No typed-email gate.** The customer screen makes you retype your address because that
///    action is irreversible on the spot. This is a reversible request, so the confirmation dialog
///    alone is the right friction — more would be theatre.
/// 3. **No `onDeleted` and no sign-out.** The cleaner keeps working; there are jobs assigned to
///    them. The view model has no `authClient` at all, so there is no path from here to a session
///    teardown.
///
/// The "what we have to keep" list is not decoration. Both stores require the app to say what
/// survives a deletion, and for a cleaner the honest answer is that the financial record does.
struct DeleteAccountView: View {
    @StateObject private var vm: DeleteAccountViewModel
    @State private var showConfirmDialog = false

    init(client: PartnerGdprDeletionClient, snackbar: SnackbarController) {
        _vm = StateObject(wrappedValue: DeleteAccountViewModel(client: client, snackbar: snackbar))
    }

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.l) {
                    header
                    if !vm.requested {
                        whatIsKept
                    }
                }
                .padding(Spacing.m)
            }
            .safeAreaInset(edge: .bottom) {
                if !vm.requested {
                    requestButton
                        .padding(.horizontal, Spacing.m)
                        .padding(.top, Spacing.s)
                        .padding(.bottom, Spacing.m)
                        .background(CleansiaColors.background)
                }
            }
            if showConfirmDialog {
                confirmDialog
            }
        }
        .navigationTitle(L10n.DeleteAccount.title)
        .navigationBarTitleDisplayMode(.inline)
    }

    private var header: some View {
        HStack(alignment: .top, spacing: Spacing.m) {
            ZStack {
                Circle()
                    .fill(CleansiaColors.errorContainer)
                    .frame(width: 48, height: 48)
                Image(systemName: vm.requested ? "checkmark.circle" : "trash")
                    .foregroundColor(CleansiaColors.error)
            }
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(vm.requested ? L10n.DeleteAccount.requestedHeadline : L10n.DeleteAccount.headline)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(vm.requested ? L10n.DeleteAccount.requestedBody : L10n.DeleteAccount.body)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
    }

    private var whatIsKept: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.DeleteAccount.keptLabel.uppercased())
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            VStack(alignment: .leading, spacing: Spacing.xs) {
                keptItem("doc.text", L10n.DeleteAccount.keptInvoices)
                keptItem("banknote", L10n.DeleteAccount.keptPay)
                keptItem("signature", L10n.DeleteAccount.keptAgreement)
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: 12))
        }
    }

    private func keptItem(_ icon: String, _ text: String) -> some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            Image(systemName: icon)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .frame(width: 20)
            Text(text)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
    }

    private var requestButton: some View {
        CleansiaDangerButton(
            L10n.DeleteAccount.cta,
            leadingIcon: "trash",
            loading: vm.submitState.isSubmitting,
            enabled: !vm.submitState.isSubmitting
        ) {
            // The dialog is the only gate. Never call submit() straight from here — that is how a
            // mis-tap becomes a filed request against a colleague's account on a shared device.
            showConfirmDialog = true
        }
    }

    private var confirmDialog: some View {
        CleansiaDialog(
            title: L10n.DeleteAccount.dialogTitle,
            confirmLabel: L10n.DeleteAccount.dialogConfirm,
            onConfirm: {
                showConfirmDialog = false
                Task { await vm.submit() }
            },
            onDismiss: { showConfirmDialog = false },
            message: L10n.DeleteAccount.dialogMessage,
            dismissLabel: L10n.cancel,
            icon: "exclamationmark.triangle",
            destructive: true,
            confirmEnabled: !vm.submitState.isSubmitting
        )
    }
}
