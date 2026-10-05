import CleansiaCore
import SwiftUI

struct RegistrationLockView: View {
    @StateObject private var vm: RegistrationLockViewModel
    @StateObject private var chainVM: OnboardingChainViewModel
    @StateObject private var avatarVM: ProfileAvatarViewModel
    @StateObject private var avatarCache = RemoteImageCache()
    @ObservedObject private var preferences: PreferencesModel
    @EnvironmentObject private var pushNavigation: PushNavigationModel
    @Environment(\.scenePhase) private var scenePhase
    @Environment(\.openURL) private var openURL
    @State private var path = NavigationPath()
    @State private var isConfirmingSignOut = false

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
        preferences: PreferencesModel,
        snackbar: SnackbarController,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        serviceArea: ServiceAreaProvider,
        // Defaulted rather than threaded through the shell: the live client holds nothing —
        // the generated layer carries the session — so this parameter exists for the tests,
        // not the app. Same reasoning as ProfileView's, and it sits ahead of the trailing
        // closures so no call site changes.
        userClient: PartnerUserClient = LivePartnerUserClient(),
        onCompleted: @escaping () -> Void,
        onSignedOut: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: RegistrationLockViewModel(
            client: client,
            authClient: authClient,
            legalDocumentsClient: profileClient
        ))
        _chainVM = StateObject(wrappedValue: OnboardingChainViewModel(client: profileClient))
        _avatarVM = StateObject(
            wrappedValue: ProfileAvatarViewModel(client: userClient, snackbar: snackbar)
        )
        self.preferences = preferences
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
                // Signing out is the one destructive thing this screen offers, and the button sat one
                // tap away from it on both platforms. A second sign-out while one is in flight is
                // refused by the view model's own guard.
                .alert(L10n.RegistrationLock.signOutConfirmTitle, isPresented: $isConfirmingSignOut) {
                    Button(L10n.RegistrationLock.signOut, role: .destructive) {
                        Task { await vm.signOut() }
                    }
                    Button(L10n.cancel, role: .cancel) {}
                } message: {
                    Text(L10n.RegistrationLock.signOutConfirmMessage)
                }
                .background(CleansiaColors.background.ignoresSafeArea())
                .navigationDestination(for: ProfileRoute.self, destination: sectionDestination)
        }
        .task {
            // Prime the chain completion snapshot so the "Step X of 4" header
            // + per-section dots are accurate the moment the first onboarding
            // section mounts — Android refreshes this eagerly in init
            // (OnboardingChainViewModel.kt:52-57).
            //
            // The avatar load is load-bearing, not cosmetic: ProfileAvatarViewModel.save()
            // opens with a guard on the loaded user, so without it a cleaner picks a photo,
            // taps Next, and the upload returns silently — no error, no snackbar, no photo.
            async let lock: Void = vm.load()
            async let chain: Void = chainVM.load()
            async let avatar: Void = avatarVM.load()
            _ = await (lock, chain, avatar)
        }
        .onReceive(vm.completed) { onCompleted() }
        .onReceive(vm.signedOut) { onSignedOut() }
        // The admin decides while the app sits in the background, and returning to it changes no
        // view, so neither .task nor onAppear runs again — the lock kept saying "under review" over a
        // rejection until a pull-to-refresh. Android re-checks on ON_RESUME.
        .onChange(of: scenePhase) { phase in
            if phase == .active { Task { await vm.load() } }
        }
        // A decision pushed while the lock is on screen leaves scenePhase where it was.
        .onReceive(pushNavigation.foregroundPushes.filter(Self.registrationDecisionEvents.contains)) { _ in
            Task { await vm.load() }
        }
        .onReceive(chainVM.jumpRequested) { section in
            // Replace, don't push — exactly what an advance does. The chain is a flat sequence, so a
            // jump must not grow the stack any more than moving forward does, or system-back stops
            // meaning "leave onboarding".
            let route = section.route(onboarding: true)
            if path.isEmpty {
                path.append(route)
            } else {
                path.removeLast()
                path.append(route)
            }
        }
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
        case .loading:
            // The same skeleton the gate draws, so the post-login sequence settles once —
            // skeleton to content — instead of skeleton, spinner, content. They share the block
            // colour and the 0.9s pulse rather than a type; each mirrors its own screen.
            RegistrationLockSkeleton()
        case .error:
            // Left on the spinner deliberately: .error is unreachable here today (the view model
            // never publishes it), and a skeleton that pulses forever is the one way this could
            // become a screen nobody escapes.
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case let .loaded(data):
            RegistrationLockContent(
                data: data,
                isSigningOut: vm.action.isSubmitting,
                languageSummary: PreferencesLabels.languageSummary(
                    isFollowingSystem: preferences.isFollowingSystemLanguage,
                    tag: preferences.languageTag
                ),
                onRetry: { Task { await vm.load() } },
                onFix: handleFix,
                onLanguage: { path.append(ProfileRoute.language) },
                onSignOut: { isConfirmingSignOut = true }
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
        case .legalDocuments:
            path.append(ProfileRoute.legalDocuments)
        case .approval:
            // Only a rejection is fixable, and only support can say more than the reason shown.
            var mail = URLComponents()
            mail.scheme = "mailto"
            mail.path = CleansiaWeb.contactEmail
            mail.queryItems = [URLQueryItem(name: "subject", value: L10n.RegistrationLock.supportSubject)]
            if let url = mail.url { openURL(url) }
        }
    }

    private static let registrationDecisionEvents: Set<String> = [
        "employee.registration_approved",
        "employee.registration_rejected"
    ]

    @ViewBuilder
    private func sectionDestination(_ route: ProfileRoute) -> some View {
        switch route {
        case let .personal(onboarding):
            PersonalSectionView(
                client: profileClient,
                snackbar: snackbar,
                chainVM: chainVM,
                onboarding: onboarding,
                avatar: avatarVM,
                avatarCache: avatarCache,
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
        case .legalDocuments:
            LegalDocumentsView(client: profileClient, snackbar: snackbar)
        case .language:
            LanguagePickerView(preferences: preferences, onSelected: { path.removeLast() })
        // Reachable only from the profile HUB, not from the registration-lock stack: this switch
        // drives the onboarding chain, and none of these is a step in it. .deleteAccount belongs
        // here for the same reason — a cleaner who has not finished registering has nothing filed
        // to delete, and the hub is where the request lives. -> /decisions/adr-0052
        //
        // This switch has no `default` on purpose: a new ProfileRoute must be classified here
        // deliberately rather than silently rendering nothing. That is working as intended — it is
        // what caught .deleteAccount.
        case .emergency, .jobRadius, .theme, .devices, .deleteAccount:
            EmptyView()
        }
    }
}

struct RegistrationLockContent: View {
    let data: RegistrationLockData
    let isSigningOut: Bool
    let languageSummary: String
    let onRetry: () -> Void
    let onFix: (RegistrationStep) -> Void
    let onLanguage: () -> Void
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
                // The form this screen demands is only fillable in a language the cleaner reads, and the
                // pre-auth intro that used to be the only language control is one-shot and skippable.
                LanguageRow(summary: languageSummary, action: onLanguage)
                SignOutButton(isSigningOut: isSigningOut, action: onSignOut)
            }
            .padding(.vertical, Spacing.xl)
        }
        .refreshable { onRetry() }
    }
}

private struct LanguageRow: View {
    let summary: String
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: Spacing.s) {
                Image(systemName: "globe")
                    .font(.system(size: 20))
                    .foregroundColor(CleansiaColors.primary)
                Text(L10n.Profile.language)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Spacer()
                Text(summary)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                Image(systemName: "chevron.right")
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .cardPadding()
        }
        .buttonStyle(.plain)
        .padding(.horizontal, Spacing.m)
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
            if isFixable(step) { onFix(step) }
        } label: {
            // The icon and the trailing status line up with the title's line, however many detail lines
            // follow it; top-aligned, the smaller "Done" and the chevron sat above the title's centre.
            HStack(alignment: .stepTitleCenter, spacing: Spacing.s) {
                Image(systemName: statusSymbol)
                    .font(.system(size: 22))
                    .foregroundColor(statusColor)
                VStack(alignment: .leading, spacing: 2) {
                    Text(categoryLabel)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .alignmentGuide(.stepTitleCenter) { $0[VerticalAlignment.center] }
                    ForEach(step.details.indices, id: \.self) { index in
                        Text(detailText(step.details[index]))
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                            .multilineTextAlignment(.leading)
                    }
                    if isRejected {
                        Text(L10n.RegistrationLock.actionContactSupport)
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(CleansiaColors.primaryText)
                            .padding(.top, Spacing.xs)
                    }
                }
                Spacer()
                if step.status == .done {
                    Text(L10n.RegistrationLock.stepComplete)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.primary)
                }
                if isFixable(step) {
                    Image(systemName: "chevron.right")
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            .cardPadding()
        }
        .buttonStyle(.plain)
        .disabled(!isFixable(step))
    }

    private func detailText(_ detail: RegistrationStepDetail) -> String {
        switch detail {
        case .documentsRequired: L10n.RegistrationLock.documentsRequired
        case .approvalRejected: L10n.RegistrationLock.approvalRejected
        case .approvalAwaitingReview: L10n.RegistrationLock.approvalAwaitingReview
        case .approvalCompleteProfileFirst: L10n.RegistrationLock.approvalCompleteProfileFirst
        case let .missingField(token): L10n.RegistrationLock.missingField(token)
        case let .rejectionReason(reason): reason
        }
    }

    private var isRejected: Bool {
        step.details.contains(.approvalRejected)
    }

    private var categoryLabel: String {
        switch step.category {
        case .profile: L10n.RegistrationLock.categoryProfile
        case .documents: L10n.RegistrationLock.categoryDocuments
        case .legalDocuments: L10n.RegistrationLock.categoryLegalDocuments
        case .approval: L10n.RegistrationLock.categoryApproval
        }
    }

    /// A rejection is drawn as one, not as the hollow circle of a step nobody has started.
    private var statusSymbol: String {
        if isRejected { return "xmark.circle.fill" }
        switch step.status {
        case .done: return "checkmark.circle.fill"
        case .pending: return "hourglass"
        case .missing: return "circle"
        }
    }

    private var statusColor: Color {
        if isRejected { return CleansiaColors.error }
        switch step.status {
        case .done: return CleansiaColors.primary
        case .pending: return CleansiaColors.onSurfaceVariant
        case .missing: return CleansiaColors.onSurfaceVariant
        }
    }
}

private extension VerticalAlignment {
    /// The centre of a step row's title line.
    enum StepTitleCenter: AlignmentID {
        static func defaultValue(in context: ViewDimensions) -> CGFloat {
            context[VerticalAlignment.center]
        }
    }

    static let stepTitleCenter = VerticalAlignment(StepTitleCenter.self)
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
                    languageSummary: "Українська",
                    onRetry: {},
                    onFix: { _ in },
                    onLanguage: {},
                    onSignOut: {}
                )
                .previewDisplayName("Locked")
                RegistrationLockContent(
                    data: sample(.awaitingReview),
                    isSigningOut: false,
                    languageSummary: "Українська",
                    onRetry: {},
                    onFix: { _ in },
                    onLanguage: {},
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
