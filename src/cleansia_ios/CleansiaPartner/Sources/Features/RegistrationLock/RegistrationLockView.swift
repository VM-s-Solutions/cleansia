import CleansiaCore
import SwiftUI

struct RegistrationLockView: View {
    @StateObject private var vm: RegistrationLockViewModel
    let onCompleted: () -> Void
    let onSignedOut: () -> Void

    init(
        client: PartnerRegistrationClient,
        authClient: AuthClient,
        onCompleted: @escaping () -> Void,
        onSignedOut: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: RegistrationLockViewModel(client: client, authClient: authClient))
        self.onCompleted = onCompleted
        self.onSignedOut = onSignedOut
    }

    var body: some View {
        content
            .background(CleansiaColors.background.ignoresSafeArea())
            .task { await vm.load() }
            .onReceive(vm.completed) { onCompleted() }
            .onReceive(vm.signedOut) { onSignedOut() }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading, .error:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case let .loaded(data):
            RegistrationLockContent(
                data: data,
                isSigningOut: vm.action.isSubmitting,
                onRetry: { Task { await vm.load() } },
                onSignOut: { Task { await vm.signOut() } }
            )
        }
    }
}

struct RegistrationLockContent: View {
    let data: RegistrationLockData
    let isSigningOut: Bool
    let onRetry: () -> Void
    let onSignOut: () -> Void

    var body: some View {
        ScrollView {
            VStack(spacing: Spacing.m) {
                LockHero()
                ProgressBanner(completed: data.completedCount, total: data.totalCount)
                if let errorMessage = data.errorMessage {
                    ErrorBanner(message: errorMessage, onRetry: onRetry)
                }
                VStack(spacing: Spacing.s) {
                    ForEach(data.steps.indices, id: \.self) { index in
                        StepRow(step: data.steps[index])
                    }
                }
                .padding(.horizontal, Spacing.m)
                SignOutButton(isSigningOut: isSigningOut, action: onSignOut)
            }
            .padding(.vertical, Spacing.xl)
        }
        .refreshable { onRetry() }
    }
}

private struct LockHero: View {
    var body: some View {
        VStack(spacing: Spacing.s) {
            Image(systemName: "lock.fill")
                .font(.system(size: 44))
                .foregroundColor(CleansiaColors.primary)
            Text(L10n.RegistrationLock.title)
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.onBackground)
                .multilineTextAlignment(.center)
            Text(L10n.RegistrationLock.subtitle)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
        }
        .padding(.horizontal, Spacing.m)
    }
}

private struct ProgressBanner: View {
    let completed: Int
    let total: Int

    var body: some View {
        VStack(spacing: Spacing.xs) {
            ProgressView(value: total > 0 ? Double(completed) / Double(total) : 0)
                .tint(CleansiaColors.primary)
            Text(L10n.RegistrationLock.progress(completed, total))
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .padding(.horizontal, Spacing.m)
    }
}

private struct ErrorBanner: View {
    let message: String
    let onRetry: () -> Void

    var body: some View {
        HStack(spacing: Spacing.s) {
            Image(systemName: "exclamationmark.triangle")
                .foregroundColor(CleansiaColors.error)
            Text(message)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Spacer()
            CleansiaOutlinedButton(L10n.RegistrationLock.retry, size: .small, action: onRetry)
                .fixedSize()
        }
        .cardPadding()
        .padding(.horizontal, Spacing.m)
    }
}

private struct StepRow: View {
    let step: RegistrationStep

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            Image(systemName: statusSymbol)
                .font(.system(size: 22))
                .foregroundColor(statusColor)
            VStack(alignment: .leading, spacing: 2) {
                Text(categoryLabel)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                ForEach(step.details.indices, id: \.self) { index in
                    Text(detailText(step.details[index]))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.leading)
                }
            }
            Spacer()
            if step.status == .done {
                Text(L10n.RegistrationLock.stepComplete)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.primary)
            } else if fixActionLabel != nil {
                Image(systemName: "chevron.right")
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
        .cardPadding()
    }

    private func detailText(_ detail: RegistrationStepDetail) -> String {
        switch detail {
        case .documentsRequired: L10n.RegistrationLock.documentsRequired
        case .approvalRejected: L10n.RegistrationLock.approvalRejected
        case .approvalAwaitingReview: L10n.RegistrationLock.approvalAwaitingReview
        case .approvalCompleteProfileFirst: L10n.RegistrationLock.approvalCompleteProfileFirst
        case let .missingField(token): L10n.RegistrationLock.missingField(token)
        }
    }

    private var categoryLabel: String {
        switch step.category {
        case .profile: L10n.RegistrationLock.categoryProfile
        case .documents: L10n.RegistrationLock.categoryDocuments
        case .approval: L10n.RegistrationLock.categoryApproval
        }
    }

    private var fixActionLabel: String? {
        guard step.status != .done else { return nil }
        switch step.category {
        case .profile: return L10n.RegistrationLock.actionCompleteProfile
        case .documents: return L10n.RegistrationLock.actionUploadDocuments
        case .approval: return nil
        }
    }

    private var statusSymbol: String {
        switch step.status {
        case .done: "checkmark.circle.fill"
        case .pending: "hourglass"
        case .missing: "circle"
        }
    }

    private var statusColor: Color {
        switch step.status {
        case .done: CleansiaColors.primary
        case .pending: CleansiaColors.onSurfaceVariant
        case .missing: CleansiaColors.onSurfaceVariant
        }
    }
}

private struct SignOutButton: View {
    let isSigningOut: Bool
    let action: () -> Void

    var body: some View {
        CleansiaOutlinedButton(
            L10n.RegistrationLock.signOut,
            size: .medium,
            enabled: !isSigningOut,
            action: action
        )
        .padding(.horizontal, Spacing.m)
    }
}

#if DEBUG
    struct RegistrationLockView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                RegistrationLockContent(data: sample(.locked), isSigningOut: false, onRetry: {}, onSignOut: {})
                    .previewDisplayName("Locked")
                RegistrationLockContent(data: sample(.awaitingReview), isSigningOut: false, onRetry: {}, onSignOut: {})
                    .previewDisplayName("Awaiting review")
                RegistrationLockContent(data: sample(.rejected), isSigningOut: false, onRetry: {}, onSignOut: {})
                    .previewDisplayName("Rejected")
                RegistrationLockContent(data: sample(.error), isSigningOut: false, onRetry: {}, onSignOut: {})
                    .previewDisplayName("Error banner")
            }
            .background(CleansiaColors.background)
        }

        private enum Variant { case locked, awaitingReview, rejected, error }

        private static func sample(_ variant: Variant) -> RegistrationLockData {
            let steps: [RegistrationStep]
            var error: String?
            switch variant {
            case .locked:
                steps = [
                    RegistrationStep(
                        category: .profile,
                        status: .missing,
                        details: [.missingField("profile.fields.firstName"), .missingField("profile.fields.iban")]
                    ),
                    RegistrationStep(category: .documents, status: .missing, details: [.documentsRequired]),
                    RegistrationStep(category: .approval, status: .missing, details: [.approvalCompleteProfileFirst])
                ]
            case .awaitingReview:
                steps = [
                    RegistrationStep(category: .profile, status: .done, details: []),
                    RegistrationStep(category: .documents, status: .done, details: []),
                    RegistrationStep(category: .approval, status: .pending, details: [.approvalAwaitingReview])
                ]
            case .rejected:
                steps = [
                    RegistrationStep(category: .profile, status: .done, details: []),
                    RegistrationStep(category: .documents, status: .done, details: []),
                    RegistrationStep(category: .approval, status: .missing, details: [.approvalRejected])
                ]
            case .error:
                steps = [
                    RegistrationStep(category: .profile, status: .missing, details: []),
                    RegistrationStep(category: .documents, status: .missing, details: []),
                    RegistrationStep(category: .approval, status: .missing, details: [])
                ]
                error = "Failed to load setup status."
            }
            return RegistrationLockData(steps: steps, errorMessage: error, isComplete: false)
        }
    }
#endif
