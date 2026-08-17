import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct ProfileView: View {
    @StateObject private var vm: ProfileViewModel
    @StateObject private var avatarVM: ProfileAvatarViewModel
    @StateObject private var chainVM: OnboardingChainViewModel
    @ObservedObject private var preferences: PreferencesModel
    /// Scoped to this screen because the avatar is the only remote image the partner app caches, and it
    /// is the cleaner's own: the hub is torn down with the shell at sign-out, and the cache with it.
    @StateObject private var avatarCache = RemoteImageCache()
    @State private var path = NavigationPath()
    @State private var showLogoutDialog = false

    private let client: PartnerProfileClient
    private let devicesClient: PartnerDevicesClient
    private let authClient: AuthClient
    private let snackbar: SnackbarController
    private let geocoding: GeocodingService
    private let mapProvider: MapProvider
    private let serviceArea: ServiceAreaProvider
    private let onSignedOut: () -> Void

    init(
        client: PartnerProfileClient,
        devicesClient: PartnerDevicesClient,
        authClient: AuthClient,
        snackbar: SnackbarController,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        serviceArea: ServiceAreaProvider,
        preferences: PreferencesModel,
        // Defaulted rather than threaded through the shell: the live client holds nothing — the
        // generated layer carries the session — so this parameter exists for the tests, not the app.
        userClient: PartnerUserClient = LivePartnerUserClient(),
        onSignedOut: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: ProfileViewModel(
            client: client,
            authClient: authClient,
            snackbar: snackbar
        ))
        _avatarVM = StateObject(wrappedValue: ProfileAvatarViewModel(client: userClient, snackbar: snackbar))
        _chainVM = StateObject(wrappedValue: OnboardingChainViewModel(client: client))
        self.preferences = preferences
        self.client = client
        self.devicesClient = devicesClient
        self.authClient = authClient
        self.snackbar = snackbar
        self.geocoding = geocoding
        self.mapProvider = mapProvider
        self.serviceArea = serviceArea
        self.onSignedOut = onSignedOut
    }

    var body: some View {
        NavigationStack(path: $path) {
            // Bar hidden at the hub root only — the gradient hero runs under the
            // status bar (customer-shell pattern); every pushed destination sets
            // its own title, so their bars stay visible.
            content
                .toolbar(.hidden, for: .navigationBar)
                .navigationDestination(for: ProfileRoute.self, destination: destination)
        }
        .task { await vm.load() }
        // Its own task, not a second await inside the hub's: the avatar comes from a different endpoint
        // and neither read is worth making the other wait for.
        .task { await avatarVM.load() }
        .onReceive(vm.signedOut) { onSignedOut() }
        .overlay { logoutOverlay }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(CleansiaColors.background.ignoresSafeArea())
        case .error:
            ErrorContent(onRetry: { Task { await vm.load() } })
        case let .loaded(data):
            ProfileHubContent(
                data: data,
                avatar: avatarVM,
                avatarCache: avatarCache,
                languageSummary: PreferencesLabels.languageSummary(
                    isFollowingSystem: preferences.isFollowingSystemLanguage,
                    tag: preferences.languageTag
                ),
                themeSummary: PreferencesLabels.themeLabel(preferences.theme),
                onOpen: { route in path.append(route) },
                onLogout: { showLogoutDialog = true }
            )
        }
    }

    @ViewBuilder
    private func destination(_ route: ProfileRoute) -> some View {
        switch route {
        case let .personal(onboarding):
            PersonalSectionView(
                client: client,
                snackbar: snackbar,
                chainVM: chainVM,
                onboarding: onboarding,
                onSaved: { popOrAdvance(onboarding: onboarding) }
            )
        case let .address(onboarding):
            AddressSectionView(
                client: client,
                snackbar: snackbar,
                chainVM: chainVM,
                geocoding: geocoding,
                mapProvider: mapProvider,
                serviceArea: serviceArea,
                onboarding: onboarding,
                onSaved: { popOrAdvance(onboarding: onboarding) }
            )
        case let .identification(onboarding):
            IdentificationSectionView(
                client: client,
                snackbar: snackbar,
                chainVM: chainVM,
                onboarding: onboarding,
                onSaved: { popOrAdvance(onboarding: onboarding) }
            )
        case let .bank(onboarding):
            BankSectionView(
                client: client,
                snackbar: snackbar,
                chainVM: chainVM,
                onboarding: onboarding,
                onSaved: { popOrAdvance(onboarding: onboarding) }
            )
        default:
            secondaryDestination(route)
        }
    }

    @ViewBuilder
    private func secondaryDestination(_ route: ProfileRoute) -> some View {
        switch route {
        case .emergency:
            EmergencySectionView(client: client, snackbar: snackbar, onSaved: { popLast() })
        case .documents:
            DocumentsSectionView(client: client, snackbar: snackbar)
        case .jobRadius:
            JobRadiusSectionView(
                client: client,
                snackbar: snackbar,
                onSaved: { radiusKm in
                    vm.applyJobRadius(radiusKm)
                    popLast()
                }
            )
        case .devices:
            DevicesView(
                client: devicesClient,
                authClient: authClient,
                snackbar: snackbar,
                onSignedOut: onSignedOut
            )
        case .language:
            LanguagePickerView(preferences: preferences, onSelected: { popLast() })
        case .theme:
            ThemePickerView(preferences: preferences, onSelected: { popLast() })
        default:
            EmptyView()
        }
    }

    private func popLast() {
        if !path.isEmpty { path.removeLast() }
    }

    private func popOrAdvance(onboarding _: Bool) {
        // Maintenance edits (onboarding == false) simply pop. The onboarding
        // chain only runs from the registration lock's own stack, never the hub.
        popLast()
    }

    @ViewBuilder
    private var logoutOverlay: some View {
        if showLogoutDialog {
            CleansiaDialog(
                title: L10n.Profile.logoutDialogTitle,
                confirmLabel: L10n.Profile.logoutDialogConfirm,
                onConfirm: {
                    showLogoutDialog = false
                    Task { await vm.signOut() }
                },
                onDismiss: { showLogoutDialog = false },
                message: L10n.Profile.logoutDialogMessage,
                dismissLabel: L10n.Profile.logoutDialogCancel,
                icon: "rectangle.portrait.and.arrow.right",
                destructive: true,
                confirmEnabled: !vm.action.isSubmitting
            )
        }
    }
}

private struct ErrorContent: View {
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Text(L10n.Profile.errorGeneric)
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            CleansiaOutlinedButton(L10n.retry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}
