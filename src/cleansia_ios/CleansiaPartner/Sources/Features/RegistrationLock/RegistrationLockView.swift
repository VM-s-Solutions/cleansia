import CleansiaCore
import SwiftUI

struct RegistrationLockView: View {
    @StateObject private var vm: RegistrationLockViewModel
    @StateObject private var chainVM: OnboardingChainViewModel
    @State private var path = NavigationPath()

    let onCompleted: () -> Void
    let onSignedOut: () -> Void

    private let profileClient: PartnerProfileClient
    private let snackbar: SnackbarController
    private let geocoding: GeocodingService
    private let mapProvider: MapProvider
    private let serviceArea: ServiceAreaProvider

    init(
        client: PartnerRegistrationClient,
        authClient: AuthClient,
        profileClient: PartnerProfileClient,
        snackbar: SnackbarController,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        serviceArea: ServiceAreaProvider,
        onCompleted: @escaping () -> Void,
        onSignedOut: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: RegistrationLockViewModel(client: client, authClient: authClient))
        _chainVM = StateObject(wrappedValue: OnboardingChainViewModel(client: profileClient))
        self.profileClient = profileClient
        self.snackbar = snackbar
        self.geocoding = geocoding
        self.mapProvider = mapProvider
        self.serviceArea = serviceArea
        self.onCompleted = onCompleted
        self.onSignedOut = onSignedOut
    }

    var body: some View {
        NavigationStack(path: $path) {
            content
                .background(CleansiaColors.background.ignoresSafeArea())
                .navigationDestination(for: ProfileRoute.self, destination: sectionDestination)
        }
        .task {
            // Prime the chain completion snapshot so the "Step X of 4" header
            // + per-section dots are accurate the moment the first onboarding
            // section mounts — Android refreshes this eagerly in init
            // (OnboardingChainViewModel.kt:52-57).
            async let lock: Void = vm.load()
            async let chain: Void = chainVM.load()
            _ = await (lock, chain)
        }
        .onReceive(vm.completed) { onCompleted() }
        .onReceive(vm.signedOut) { onSignedOut() }
        .onReceive(chainVM.advanced) { step in
            switch step {
            case let .next(route):
                // Replace the current section with the next missing one so
                // system-back returns to the lock, not the previous section.
                if !path.isEmpty {
                    path.removeLast()
                    path.append(route)
                }
            case .finished:
                // Chain done — pop back to the lock; its onAppear re-load
                // re-resolves and only the success watermark flips the root.
                path.removeLast(path.count)
            }
        }
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
                onFix: handleFix,
                onSignOut: { Task { await vm.signOut() } }
            )
            // Re-resolve the gate whenever the lock surfaces again (e.g. after a
            // section save pops back) — only isComplete flips the root.
            .onAppear { Task { await vm.load() } }
        }
    }

    private func handleFix(_ step: RegistrationStep) {
        switch step.category {
        case .profile:
            path.append(
                ProfileSectionRouting.firstMissingSection(
                    missingFields: vm.missingFields,
                    forOnboarding: true
                )
            )
        case .documents:
            path.append(ProfileRoute.documents)
        case .approval:
            break
        }
    }

    @ViewBuilder
    private func sectionDestination(_ route: ProfileRoute) -> some View {
        switch route {
        case let .personal(onboarding):
            PersonalSectionView(
                client: profileClient,
                snackbar: snackbar,
                chainVM: chainVM,
                onboarding: onboarding,
                onSaved: { Task { await chainVM.advanceOrFinish() } }
            )
        case let .address(onboarding):
            AddressSectionView(
                client: profileClient,
                snackbar: snackbar,
                chainVM: chainVM,
                geocoding: geocoding,
                mapProvider: mapProvider,
                serviceArea: serviceArea,
                onboarding: onboarding,
                onSaved: { Task { await chainVM.advanceOrFinish() } }
            )
        case let .identification(onboarding):
            IdentificationSectionView(
                client: profileClient,
                snackbar: snackbar,
                chainVM: chainVM,
                onboarding: onboarding,
                onSaved: { Task { await chainVM.advanceOrFinish() } }
            )
        case let .bank(onboarding):
            BankSectionView(
                client: profileClient,
                snackbar: snackbar,
                chainVM: chainVM,
                onboarding: onboarding,
                onSaved: { Task { await chainVM.advanceOrFinish() } }
            )
        case .documents:
            DocumentsSectionView(client: profileClient, snackbar: snackbar)
        case .emergency, .language, .theme, .devices:
            EmptyView()
        }
    }
}

struct RegistrationLockContent: View {
    let data: RegistrationLockData
    let isSigningOut: Bool
    let onRetry: () -> Void
    let onFix: (RegistrationStep) -> Void
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
                        StepRow(step: data.steps[index], onFix: onFix)
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
    let onFix: (RegistrationStep) -> Void

    var body: some View {
        Button {
            if canFix { onFix(step) }
        } label: {
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
                } else if canFix {
                    Image(systemName: "chevron.right")
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            .cardPadding()
        }
        .buttonStyle(.plain)
        .disabled(!canFix)
    }

    private var canFix: Bool {
        guard step.status != .done else { return false }
        switch step.category {
        case .profile, .documents: return true
        case .approval: return false
        }
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
                RegistrationLockContent(
                    data: sample(.locked),
                    isSigningOut: false,
                    onRetry: {},
                    onFix: { _ in },
                    onSignOut: {}
                )
                .previewDisplayName("Locked")
                RegistrationLockContent(
                    data: sample(.awaitingReview),
                    isSigningOut: false,
                    onRetry: {},
                    onFix: { _ in },
                    onSignOut: {}
                )
                .previewDisplayName("Awaiting review")
            }
            .background(CleansiaColors.background)
        }

        private enum Variant { case locked, awaitingReview }

        private static func sample(_ variant: Variant) -> RegistrationLockData {
            switch variant {
            case .locked:
                RegistrationLockData(
                    steps: [
                        RegistrationStep(
                            category: .profile,
                            status: .missing,
                            details: [.missingField("profile.fields.firstName")]
                        ),
                        RegistrationStep(category: .documents, status: .missing, details: [.documentsRequired]),
                        RegistrationStep(
                            category: .approval,
                            status: .missing,
                            details: [.approvalCompleteProfileFirst]
                        )
                    ],
                    errorMessage: nil,
                    isComplete: false
                )
            case .awaitingReview:
                RegistrationLockData(
                    steps: [
                        RegistrationStep(category: .profile, status: .done, details: []),
                        RegistrationStep(category: .documents, status: .done, details: []),
                        RegistrationStep(category: .approval, status: .pending, details: [.approvalAwaitingReview])
                    ],
                    errorMessage: nil,
                    isComplete: false
                )
            }
        }
    }
#endif
