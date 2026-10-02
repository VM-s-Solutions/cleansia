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
                    whatIsKept
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
        }
        .alert(L10n.DeleteAccount.dialogTitle, isPresented: $showConfirmDialog) {
            Button(L10n.DeleteAccount.dialogConfirm, role: .destructive) {
                Task { await vm.confirmDelete() }
            }
            Button(L10n.cancel, role: .cancel) {}
        } message: {
            Text(L10n.DeleteAccount.dialogMessage)
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
        recordList(L10n.DeleteAccount.whatHappens, [
            L10n.DeleteAccount.itemProfile,
            L10n.DeleteAccount.itemAddresses,
            L10n.DeleteAccount.itemHistory,
            L10n.DeleteAccount.itemDevices
        ])
    }

    private var whatIsKept: some View {
        recordList(L10n.DeleteAccount.whatIsKept, [
            L10n.DeleteAccount.keptBookings,
            L10n.DeleteAccount.keptReceipts,
            L10n.DeleteAccount.keptConsents,
            L10n.DeleteAccount.keptAudit,
            L10n.DeleteAccount.keptDisputes
        ])
    }

    private func recordList(_ title: String, _ items: [String]) -> some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Text(title.uppercased())
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            VStack(alignment: .leading, spacing: Spacing.xs) {
                ForEach(items, id: \.self) { item in
                    Text("•  \(item)")
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                }
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        }
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
}
