import CleansiaCore
import SwiftUI

struct DeleteAccountView: View {
    @StateObject private var vm: DeleteAccountViewModel
    @State private var typedEmail = ""
    @State private var showConfirmDialog = false

    private let userEmail: String
    private let onDeleted: () -> Void

    init(
        userEmail: String,
        client: GdprDeleteClient,
        authClient: AuthClient,
        snackbar: SnackbarController,
        onDeleted: @escaping () -> Void
    ) {
        self.userEmail = userEmail
        self.onDeleted = onDeleted
        _vm = StateObject(wrappedValue: DeleteAccountViewModel(
            client: client,
            authClient: authClient,
            snackbar: snackbar
        ))
    }

    private var emailMatches: Bool {
        !typedEmail.isBlank && typedEmail.trimmingCharacters(in: .whitespacesAndNewlines)
            .caseInsensitiveCompare(userEmail) == .orderedSame
    }

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.l) {
                    header
                    whatGetsDeleted
                    appleNote
                    confirmField
                }
                .padding(Spacing.m)
            }
            .safeAreaInset(edge: .bottom) {
                deleteButton
                    .padding(.horizontal, Spacing.m)
                    .padding(.top, Spacing.s)
                    .padding(.bottom, Spacing.m)
                    .background(CleansiaColors.background)
            }
            if showConfirmDialog {
                confirmDialog
            }
        }
        .navigationTitle(L10n.DeleteAccount.title)
        .navigationBarTitleDisplayMode(.inline)
        .onReceive(vm.accountDeleted) { onDeleted() }
    }

    private var header: some View {
        HStack(alignment: .top, spacing: Spacing.m) {
            ZStack {
                Circle()
                    .fill(CleansiaColors.errorContainer)
                    .frame(width: 48, height: 48)
                Image(systemName: "trash")
                    .foregroundColor(CleansiaColors.error)
            }
            Text(L10n.DeleteAccount.subtitle)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
    }

    private var whatGetsDeleted: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.DeleteAccount.whatHappens.uppercased())
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            VStack(alignment: .leading, spacing: Spacing.xs) {
                deletedItem(L10n.DeleteAccount.itemProfile)
                deletedItem(L10n.DeleteAccount.itemAddresses)
                deletedItem(L10n.DeleteAccount.itemHistory)
                deletedItem(L10n.DeleteAccount.itemDevices)
                deletedItem(L10n.DeleteAccount.itemConsents)
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        }
    }

    private func deletedItem(_ text: String) -> some View {
        Text("•  \(text)")
            .font(CleansiaTypography.bodyMedium)
            .foregroundColor(CleansiaColors.onSurface)
    }

    private var appleNote: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            Image(systemName: "apple.logo")
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(L10n.DeleteAccount.appleRevokeNote)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surfaceVariant)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }

    private var confirmField: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.DeleteAccount.confirmLabel)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            CleansiaTextField(
                value: $typedEmail,
                label: L10n.DeleteAccount.confirmHint,
                errorText: (!typedEmail.isBlank && !emailMatches) ? L10n.DeleteAccount.confirmMismatch : nil,
                keyboardType: .emailAddress,
                textContentType: .emailAddress
            )
        }
    }

    private var deleteButton: some View {
        CleansiaDangerButton(
            L10n.DeleteAccount.confirmButton,
            leadingIcon: "trash",
            loading: vm.deleteState.isSubmitting,
            enabled: emailMatches && !vm.deleteState.isSubmitting
        ) {
            showConfirmDialog = true
        }
    }

    private var confirmDialog: some View {
        CleansiaDialog(
            title: L10n.DeleteAccount.dialogTitle,
            confirmLabel: L10n.DeleteAccount.dialogConfirm,
            onConfirm: {
                showConfirmDialog = false
                Task { await vm.confirmDelete() }
            },
            onDismiss: { showConfirmDialog = false },
            message: L10n.DeleteAccount.dialogMessage,
            dismissLabel: L10n.cancel,
            icon: "exclamationmark.triangle",
            destructive: true,
            confirmEnabled: !vm.deleteState.isSubmitting
        )
    }
}
