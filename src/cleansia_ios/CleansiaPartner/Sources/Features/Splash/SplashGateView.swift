import CleansiaCore
import SwiftUI

struct SplashGateView: View {
    @StateObject private var vm: SplashViewModel
    let onResolved: (SplashOutcome) -> Void
    let onSignOut: () -> Void

    init(
        hasValidSession: Bool,
        settings: AppSettingsStore,
        client: PartnerRegistrationClient,
        showsBrandHold: Bool = true,
        onSignOut: @escaping () -> Void,
        onResolved: @escaping (SplashOutcome) -> Void
    ) {
        _vm = StateObject(wrappedValue: SplashViewModel(
            hasValidSession: hasValidSession,
            settings: settings,
            client: client,
            // Not a shorter hold — none at all. A hold that is merely shorter still reads as a
            // second launch; the gate should be invisible when it is fast.
            hold: showsBrandHold ? SplashViewModel.brandHold : {}
        ))
        self.onResolved = onResolved
        self.onSignOut = onSignOut
    }

    var body: some View {
        ZStack {
            if vm.outcome == .unreachable {
                SplashUnreachableView(onRetry: { Task { await vm.resolve() } }, onSignOut: onSignOut)
            } else {
                WordmarkSplashView(subtitle: L10n.Splash.tagline, showsPartnerLabel: true)
            }
        }
        .task { await vm.resolve() }
        .onChange(of: vm.outcome) { outcome in
            // `.unreachable` is the one outcome that resolves to STAYING here. Handing it to
            // `onResolved` would send it through `Route.afterSplash`, which is the navigation this
            // whole change exists to avoid.
            guard let outcome, outcome != .unreachable else { return }
            onResolved(outcome)
        }
    }
}

/// Mirrors `DashboardErrorView`'s shape rather than reaching for a shared component, because the
/// tree has no shared error view and inventing one from the splash is not the place to start.
///
/// Carries a sign-out as well as a retry: the splash has no back stack, and signing out is the one
/// thing a cleaner stuck here can always do — the same escape the registration lock offers.
private struct SplashUnreachableView: View {
    let onRetry: () -> Void
    let onSignOut: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Image(systemName: "wifi.exclamationmark")
                .font(.system(size: 40))
                .foregroundColor(CleansiaColors.error)
            Text(L10n.Splash.unreachableTitle)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .multilineTextAlignment(.center)
            Text(L10n.Splash.unreachableMessage)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaOutlinedButton(L10n.retry, size: .medium, action: onRetry)
                .fixedSize()
            Button(L10n.Profile.logout, action: onSignOut)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background)
    }
}

#if DEBUG
    struct SplashGateView_Previews: PreviewProvider {
        static var previews: some View {
            WordmarkSplashView(subtitle: "Manage your jobs on the go", showsPartnerLabel: true)
                .previewDisplayName("Resolving")
        }
    }
#endif
