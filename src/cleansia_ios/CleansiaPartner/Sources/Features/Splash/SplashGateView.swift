import CleansiaCore
import SwiftUI

struct SplashGateView: View {
    @StateObject private var vm: SplashViewModel
    let onResolved: (SplashOutcome) -> Void
    let onSignOut: () -> Void

    /// Whether to draw the branded wordmark at all. False on every entry that is not a cold start —
    /// a login, a confirmed email, a retry — where the cleaner has just been looking at the app and
    /// the reveal has nothing left to introduce. The GATE still runs either way (ADR-0020 D3); this
    /// only decides what is on screen while it does.
    private let showsBrandReveal: Bool

    init(
        hasValidSession: Bool,
        settings: AppSettingsStore,
        client: PartnerRegistrationClient,
        showsBrandReveal: Bool = true,
        onSignOut: @escaping () -> Void,
        onResolved: @escaping (SplashOutcome) -> Void
    ) {
        self.showsBrandReveal = showsBrandReveal
        _vm = StateObject(wrappedValue: SplashViewModel(
            hasValidSession: hasValidSession,
            settings: settings,
            client: client,
            // Not a shorter hold — none at all. A hold that is merely shorter still reads as a
            // second launch; the gate should be invisible when it is fast.
            hold: showsBrandReveal ? SplashViewModel.brandHold : {}
        ))
        self.onResolved = onResolved
        self.onSignOut = onSignOut
    }

    var body: some View {
        ZStack {
            if vm.outcome == .unreachable {
                SplashUnreachableView(onRetry: { Task { await vm.resolve() } }, onSignOut: onSignOut)
            } else if showsBrandReveal {
                WordmarkSplashView(subtitle: L10n.Splash.tagline, showsPartnerLabel: true)
            } else {
                // Removing the 1.8s hold was not enough: the branded splash was still DRAWN for as
                // long as the round-trip took, so signing in still looked like the app relaunching.
                // The gate is unchanged; it just stops announcing itself.
                RegistrationLockSkeleton()
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

/// The shape the registration lock is about to take — hero, progress banner, four step rows.
///
/// Deliberately NOT a shared component. `RegistrationLockView` has its own copy for its own loading
/// state, and the two read as the same material because they share the block colour and the 0.9s
/// pulse, not because they share a type. Extracting one would put a gate-shaped view on a non-gate
/// screen and give the next reader a component whose two callers want it to look like two things.
/// Follows the customer app's `HomeSkeleton`.
struct RegistrationLockSkeleton: View {
    @State private var pulsing = false

    var body: some View {
        VStack(spacing: Spacing.m) {
            Spacer().frame(height: Spacing.xl)
            Circle()
                .fill(blockColor)
                .frame(width: 44, height: 44)
            block(height: 24, radius: CornerRadius.extraSmall).frame(width: 220)
            block(height: 16, radius: CornerRadius.extraSmall).frame(width: 280)

            Spacer().frame(height: Spacing.s)
            block(height: 6, radius: 3)

            VStack(spacing: Spacing.s) {
                ForEach(0 ..< 4, id: \.self) { _ in
                    block(height: 72, radius: CornerRadius.medium)
                }
            }
            Spacer()
        }
        .padding(.horizontal, Spacing.m)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background)
        .onAppear {
            withAnimation(.easeInOut(duration: 0.9).repeatForever(autoreverses: true)) {
                pulsing = true
            }
        }
        .accessibilityHidden(true)
    }

    private var blockColor: Color {
        CleansiaColors.outlineVariant.opacity(pulsing ? 0.6 : 0.3)
    }

    private func block(height: CGFloat, radius: CGFloat) -> some View {
        RoundedRectangle(cornerRadius: radius)
            .fill(blockColor)
            .frame(maxWidth: .infinity)
            .frame(height: height)
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
